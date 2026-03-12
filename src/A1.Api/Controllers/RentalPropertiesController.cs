using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RentalPropertiesController : ControllerBase
    {
        private readonly IGenericRepository<RentalProperty> _repository;
        private readonly ApplicationDbContext _context;

        public RentalPropertiesController(IGenericRepository<RentalProperty> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 200) pageSize = 200;

            var baseQuery = _context.RentalProperties
                .AsNoTracking()
                .Where(r => r.IsDeleted == null || r.IsDeleted == false);
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            var payload = await (from r in baseQuery
                                 join cmd in _context.Commands.Where(cmd => cmd.IsDeleted == null || cmd.IsDeleted == false)
                                     on r.CmdId equals cmd.Id into cmdGroup
                                 from cmd in cmdGroup.DefaultIfEmpty()
                                 join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                     on r.BaseId equals b.Id into baseGroup
                                 from b in baseGroup.DefaultIfEmpty()
                                 join cls in _context.Classes.Where(cls => cls.IsDeleted == null || cls.IsDeleted == false)
                                     on r.ClassId equals cls.Id into classGroup
                                 from cls in classGroup.DefaultIfEmpty()
                                 orderby r.Id descending
                                 select new RentalPropertyDto
                                 {
                                     Id = r.Id,
                                     CmdId = r.CmdId,
                                     CmdName = cmd != null ? cmd.Name : string.Empty,
                                     BaseId = r.BaseId,
                                     BaseName = b != null ? b.Name : string.Empty,
                                     ClassId = r.ClassId,
                                     ClassName = cls != null ? cls.Name : string.Empty,
                                     PId = r.PId,
                                     UoM = r.UoM,
                                     Area = r.Area,
                                     Location = r.Location,
                                     Remarks = r.Remarks,
                                     Status = r.Status,
                                     PropertyType = r.PropertyType,
                                     ActionDate = r.ActionDate,
                                     ActionBy = r.ActionBy,
                                     Action = r.Action,
                                     IsDeleted = r.IsDeleted
                                 })
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                payload.Select(x => x.Id),
                "RentalProperties", "RentalProperty");
            var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(payload, x => x.Id, attachedIds);
            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var baseQuery = _context.RentalProperties
                .AsNoTracking()
                .Where(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            var rental = await (from r in baseQuery
                                join cmd in _context.Commands.Where(cmd => cmd.IsDeleted == null || cmd.IsDeleted == false)
                                    on r.CmdId equals cmd.Id into cmdGroup
                                from cmd in cmdGroup.DefaultIfEmpty()
                                join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                    on r.BaseId equals b.Id into baseGroup
                                from b in baseGroup.DefaultIfEmpty()
                                join cls in _context.Classes.Where(cls => cls.IsDeleted == null || cls.IsDeleted == false)
                                    on r.ClassId equals cls.Id into classGroup
                                from cls in classGroup.DefaultIfEmpty()
                                select new RentalPropertyDto
                                {
                                    Id = r.Id,
                                    CmdId = r.CmdId,
                                    CmdName = cmd != null ? cmd.Name : string.Empty,
                                    BaseId = r.BaseId,
                                    BaseName = b != null ? b.Name : string.Empty,
                                    ClassId = r.ClassId,
                                    ClassName = cls != null ? cls.Name : string.Empty,
                                    PId = r.PId,
                                    UoM = r.UoM,
                                    Area = r.Area,
                                    PropertyType = r.PropertyType,
                                    Location = r.Location,
                                    Remarks = r.Remarks,
                                    Status = r.Status,
                                    ActionDate = r.ActionDate,
                                    ActionBy = r.ActionBy,
                                    Action = r.Action,
                                    IsDeleted = r.IsDeleted
                                })
                .FirstOrDefaultAsync();

            if (rental == null)
            {
                return NotFound();
            }

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                new[] { rental.Id },
                "RentalProperties", "RentalProperty");
            return Ok(AttachmentFlagHelper.ToDictionaryWithAttachmentFlag(rental, attachedIds.Contains(rental.Id)));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RentalProperty rentalProperty)
        {
            if (rentalProperty == null)
            {
                return BadRequest("Rental property data is required.");
            }

            rentalProperty.IsDeleted = false;
            await _repository.AddAsync(rentalProperty);
            return CreatedAtAction(nameof(GetById), new { id = rentalProperty.Id }, rentalProperty);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] RentalProperty rentalProperty)
        {
            if (rentalProperty == null)
            {
                return BadRequest("Rental property data is required.");
            }

            if (rentalProperty.Id == 0)
            {
                rentalProperty.Id = id;
            }
            else if (id != rentalProperty.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.RentalProperties
                .FirstOrDefaultAsync(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Rental property not found.");
            }

            existing.CmdId = rentalProperty.CmdId;
            existing.BaseId = rentalProperty.BaseId;
            existing.ClassId = rentalProperty.ClassId;
            existing.PId = rentalProperty.PId;
            existing.UoM = rentalProperty.UoM;
            existing.Area = rentalProperty.Area;
            existing.Location = rentalProperty.Location;
            existing.Remarks = rentalProperty.Remarks;
            existing.Status = rentalProperty.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.PropertyType = rentalProperty.PropertyType;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, rentalProperty.ActionBy);
            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromBody] RentalPropertyDeleteRequest? request = null)
        {
            var rental = await _context.RentalProperties
                .FirstOrDefaultAsync(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));

            if (rental == null)
            {
                return NotFound("Rental property not found.");
            }

            var actionBy = request?.ActionBy;
            if (string.IsNullOrWhiteSpace(actionBy))
            {
                // If payload doesn't have ActionBy, preserve existing value
                var existingActionBy = await _context.RentalProperties
                    .AsNoTracking()
                    .Where(r => r.Id == id)
                    .Select(r => r.ActionBy)
                    .FirstOrDefaultAsync();
                actionBy = existingActionBy;
            }

            rental.IsDeleted = true;
            rental.Action = "DELETE";
            rental.ActionDate = DateTime.UtcNow;
            rental.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, actionBy);

            _context.RentalProperties.Update(rental);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class RentalPropertyDeleteRequest
    {
        public string? ActionBy { get; set; }
    }
}

