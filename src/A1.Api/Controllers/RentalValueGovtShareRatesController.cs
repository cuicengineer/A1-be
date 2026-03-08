using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Services;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RentalValueGovtShareRatesController : ControllerBase
    {
        private readonly IGenericRepository<RentalValueGovtShareRate> _repository;
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLogService;

        public RentalValueGovtShareRatesController(IGenericRepository<RentalValueGovtShareRate> repository, ApplicationDbContext context, IAuditLogService auditLogService)
        {
            _repository = repository;
            _context = context;
            _auditLogService = auditLogService;
        }

        private static string GetActionBy(ClaimsPrincipal? user)
        {
            var name = user?.Identity?.Name;
            if (!string.IsNullOrEmpty(name)) return name;
            var claim = user?.FindFirst(ClaimTypes.Name) ?? user?.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && !string.IsNullOrEmpty(claim.Value)) return claim.Value;
            return "System";
        }


        /// <summary>
        /// GET: Get all rental value govt share rates (only returns records where IsDeleted = false/null and Status = true)
        /// Supports pagination with pageNumber and pageSize query parameters
        /// Supports filtering by type query parameter
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, [FromQuery] int? type = null)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 200) pageSize = 200; // safety cap for high-load scenarios

            var baseQuery = _context.RentalValueGovtShareRates
                .AsNoTracking()
                .Where(r => (r.IsDeleted == null || r.IsDeleted == false) && (r.Status == true));
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            // Filter by type if provided
            if (type.HasValue)
            {
                baseQuery = baseQuery.Where(r => r.Type == type.Value);
            }

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            var items = await baseQuery
                .OrderByDescending(r => r.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                items.Select(x => x.Id));
            var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(items, x => x.Id, attachedIds);
            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var baseQuery = _context.RentalValueGovtShareRates
                .AsNoTracking()
                .Where(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);
            var item = await baseQuery.FirstOrDefaultAsync();

            if (item == null) return NotFound();
            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                new[] { item.Id });
            return Ok(AttachmentFlagHelper.ToDictionaryWithAttachmentFlag(item, attachedIds.Contains(item.Id)));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RentalValueGovtShareRate item)
        {
            if (item == null) return BadRequest("Data is required.");

            item.IsDeleted = false;
            await _repository.AddAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] RentalValueGovtShareRate item)
        {
            if (item == null) return BadRequest("Data is required.");

            if (item.Id == 0)
                item.Id = id;
            else if (item.Id != id)
                return BadRequest("ID mismatch.");

            var existing = await _context.RentalValueGovtShareRates
                .FirstOrDefaultAsync(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));
            if (existing == null) return NotFound();

            var oldValuesJson = JsonSerializer.Serialize(new
            {
                existing.ClassId,
                existing.ApplicableDate,
                existing.DeactiveDate,
                existing.Rate,
                existing.Type,
                existing.CmdId,
                existing.BaseId,
                existing.Description,
                existing.Attachments,
                existing.Status
            });

            existing.ClassId = item.ClassId;
            existing.ApplicableDate = item.ApplicableDate;
            existing.DeactiveDate = item.DeactiveDate;
            existing.Rate = item.Rate;
            existing.Type = item.Type;
            existing.CmdId = item.CmdId;
            existing.BaseId = item.BaseId;
            existing.Description = item.Description;
            existing.Attachments = item.Attachments;
            existing.Status = item.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = item.ActionBy;

            await _repository.UpdateAsync(existing);

            var newValuesJson = JsonSerializer.Serialize(new
            {
                existing.ClassId,
                existing.ApplicableDate,
                existing.DeactiveDate,
                existing.Rate,
                existing.Type,
                existing.CmdId,
                existing.BaseId,
                existing.Description,
                existing.Attachments,
                existing.Status
            });
            await _auditLogService.LogAsync(new AuditLog
            {
                EntityName = "RentalValueGovtShareRate",
                EntityId = id,
                OldValuesJson = oldValuesJson,
                NewValuesJson = newValuesJson,
                ActionBy = GetActionBy(User),
                Action = "API"
            });

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromBody] RentalValueGovtShareRateDeleteRequest? request = null)
        {
            var existing = await _context.RentalValueGovtShareRates
                .FirstOrDefaultAsync(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));
            if (existing == null) return NotFound();

            var actionBy = request?.ActionBy;
            if (string.IsNullOrWhiteSpace(actionBy))
            {
                // If payload doesn't have ActionBy, preserve existing value
                var existingActionBy = await _context.RentalValueGovtShareRates
                    .AsNoTracking()
                    .Where(r => r.Id == id)
                    .Select(r => r.ActionBy)
                    .FirstOrDefaultAsync();
                actionBy = existingActionBy;
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = actionBy;

            _context.RentalValueGovtShareRates.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class RentalValueGovtShareRateDeleteRequest
    {
        public string? ActionBy { get; set; }
    }
}

