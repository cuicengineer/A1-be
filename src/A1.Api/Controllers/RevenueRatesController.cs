using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Services;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Text.Json;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RevenueRatesController : ControllerBase
    {
        private readonly IGenericRepository<RevenueRate> _repository;
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLogService;

        public RevenueRatesController(IGenericRepository<RevenueRate> repository, ApplicationDbContext context, IAuditLogService auditLogService)
        {
            _repository = repository;
            _context = context;
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// 1 = CmdId and BaseId both > 0; 2 = CmdId > 0 and BaseId is 0 or null; 3 = both 0 or null.
        /// </summary>
        private static byte GetRateScope(int? cmdId, int? baseId)
        {
            if (cmdId.GetValueOrDefault() > 0 && baseId.GetValueOrDefault() > 0) return 1;
            if (cmdId.GetValueOrDefault() > 0) return 2;
            return 3;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 200) pageSize = 200;

            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var baseQuery = _context.RevenueRates
                .AsNoTracking()
                .Where(r => r.IsDeleted == null || r.IsDeleted == false);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            var payload = await (from r in baseQuery
                                 join p in _context.RentalProperties.Where(p => p.IsDeleted == null || p.IsDeleted == false)
                                     on r.PropertyId equals p.Id into propertyGroup
                                 from p in propertyGroup.DefaultIfEmpty()
                                 where scope.IsAhq
                                       || (string.Equals(scope.AccessLevel, "base", StringComparison.OrdinalIgnoreCase)
                                           && scope.BaseId.HasValue && p != null && p.BaseId == scope.BaseId.Value)
                                       || (string.Equals(scope.AccessLevel, "command", StringComparison.OrdinalIgnoreCase)
                                           && scope.CmdId.HasValue
                                           && p != null
                                           && p.CmdId == scope.CmdId.Value
                                           && ((scope.BaseId.HasValue && p.BaseId == scope.BaseId.Value)
                                               || (!scope.BaseId.HasValue && scope.AllowedBaseIds.Contains(p.BaseId))))
                                 join cmd in _context.Commands.Where(cmd => cmd.IsDeleted == null || cmd.IsDeleted == false)
                                     on p.CmdId equals cmd.Id into cmdGroup
                                 from cmd in cmdGroup.DefaultIfEmpty()
                                 join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                     on p.BaseId equals b.Id into baseGroup
                                 from b in baseGroup.DefaultIfEmpty()
                                 join cls in _context.Classes.Where(cls => cls.IsDeleted == null || cls.IsDeleted == false)
                                     on p.ClassId equals cls.Id into classGroup
                                 from cls in classGroup.DefaultIfEmpty()
                                 orderby r.Id descending
                                 select new RevenueRateDto
                                 {
                                     Id = r.Id,
                                     PropertyId = r.PropertyId,
                                     CmdId = r.CmdId ?? (p != null ? p.CmdId : (int?)null),
                                     CmdName = cmd != null ? cmd.Name : string.Empty,
                                     BaseId = r.BaseId ?? (p != null ? p.BaseId : (int?)null),
                                     BaseName = b != null ? b.Name : string.Empty,
                                     ClassId = p != null ? p.ClassId : (int?)null,
                                     ClassName = cls != null ? cls.Name : string.Empty,
                                     PropertyIdentifier = p != null ? p.PId : null,
                                     UoM = p != null ? p.UoM : null,
                                     Area = p != null ? p.Area : null,
                                     Location = p != null ? p.Location : null,
                                     Remarks = p != null ? p.Remarks : null,
                                     ApplicableDate = r.ApplicableDate,
                                     DeactiveDate = r.DeactiveDate,
                                     Rate = r.Rate,
                                     Attachments = r.Attachments,
                                     Status = r.Status,
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
                "RevenueRates", "RevenueRate");
            var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(payload, x => x.Id, attachedIds);
            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var revenueRate = await (from r in _context.RevenueRates
                                     .AsNoTracking()
                                     .Where(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false))
                                     join p in _context.RentalProperties.Where(p => p.IsDeleted == null || p.IsDeleted == false)
                                         on r.PropertyId equals p.Id into propertyGroup
                                     from p in propertyGroup.DefaultIfEmpty()
                                     where scope.IsAhq
                                           || (string.Equals(scope.AccessLevel, "base", StringComparison.OrdinalIgnoreCase)
                                               && scope.BaseId.HasValue && p != null && p.BaseId == scope.BaseId.Value)
                                           || (string.Equals(scope.AccessLevel, "command", StringComparison.OrdinalIgnoreCase)
                                               && scope.CmdId.HasValue
                                               && p != null
                                               && p.CmdId == scope.CmdId.Value
                                               && ((scope.BaseId.HasValue && p.BaseId == scope.BaseId.Value)
                                                   || (!scope.BaseId.HasValue && scope.AllowedBaseIds.Contains(p.BaseId))))
                                     join cmd in _context.Commands.Where(cmd => cmd.IsDeleted == null || cmd.IsDeleted == false)
                                         on p.CmdId equals cmd.Id into cmdGroup
                                     from cmd in cmdGroup.DefaultIfEmpty()
                                     join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                         on p.BaseId equals b.Id into baseGroup
                                     from b in baseGroup.DefaultIfEmpty()
                                     join cls in _context.Classes.Where(cls => cls.IsDeleted == null || cls.IsDeleted == false)
                                         on p.ClassId equals cls.Id into classGroup
                                     from cls in classGroup.DefaultIfEmpty()
                                    select new RevenueRateDto
                                    {
                                        Id = r.Id,
                                        PropertyId = r.PropertyId,
                                        CmdId = r.CmdId ?? (p != null ? p.CmdId : (int?)null),
                                        CmdName = cmd != null ? cmd.Name : string.Empty,
                                        BaseId = r.BaseId ?? (p != null ? p.BaseId : (int?)null),
                                        BaseName = b != null ? b.Name : string.Empty,
                                        ClassId = p != null ? p.ClassId : (int?)null,
                                        ClassName = cls != null ? cls.Name : string.Empty,
                                        PropertyIdentifier = p != null ? p.PId : null,
                                        UoM = p != null ? p.UoM : null,
                                        Area = p != null ? p.Area : null,
                                        Location = p != null ? p.Location : null,
                                        Remarks = p != null ? p.Remarks : null,
                                        ApplicableDate = r.ApplicableDate,
                                        DeactiveDate = r.DeactiveDate,
                                        Rate = r.Rate,
                                        Attachments = r.Attachments,
                                        Status = r.Status,
                                        ActionDate = r.ActionDate,
                                        ActionBy = r.ActionBy,
                                        Action = r.Action,
                                        IsDeleted = r.IsDeleted
                                    })
                .FirstOrDefaultAsync();

            if (revenueRate == null)
            {
                return NotFound();
            }

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                new[] { revenueRate.Id },
                "RevenueRates", "RevenueRate");
            return Ok(AttachmentFlagHelper.ToDictionaryWithAttachmentFlag(revenueRate, attachedIds.Contains(revenueRate.Id)));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RevenueRate revenueRate)
        {
            if (revenueRate == null)
            {
                return BadRequest("Revenue rate data is required.");
            }

            var hasActiveRate = await _context.RevenueRates
                .AsNoTracking()
                .AnyAsync(r =>
                    (r.IsDeleted == null || r.IsDeleted == false) &&
                    r.DeactiveDate == null &&
                    r.PropertyId == revenueRate.PropertyId &&
                    r.CmdId == revenueRate.CmdId &&
                    r.BaseId == revenueRate.BaseId);
            if (hasActiveRate)
            {
                return BadRequest("A rate already exists for this item. Please deactivate the previous rate first.");
            }

            revenueRate.IsDeleted = false;
            revenueRate.RateScope = GetRateScope(revenueRate.CmdId, revenueRate.BaseId);
            revenueRate.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, revenueRate.ActionBy);
            await _repository.AddAsync(revenueRate);
            return CreatedAtAction(nameof(GetById), new { id = revenueRate.Id }, revenueRate);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] RevenueRate revenueRate)
        {
            if (revenueRate == null)
            {
                return BadRequest("Revenue rate data is required.");
            }

            if (revenueRate.Id == 0)
            {
                revenueRate.Id = id;
            }
            else if (id != revenueRate.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.RevenueRates
                .FirstOrDefaultAsync(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Revenue rate not found.");
            }

            var oldValuesJson = JsonSerializer.Serialize(new
            {
                existing.PropertyId,
                existing.CmdId,
                existing.BaseId,
                existing.ApplicableDate,
                existing.DeactiveDate,
                existing.Rate,
                existing.Attachments,
                existing.Status
            });

            existing.PropertyId = revenueRate.PropertyId;
            existing.CmdId = revenueRate.CmdId;
            existing.BaseId = revenueRate.BaseId;
            existing.RateScope = GetRateScope(existing.CmdId, existing.BaseId);
            existing.ApplicableDate = revenueRate.ApplicableDate;
            existing.DeactiveDate = revenueRate.DeactiveDate;
            existing.Rate = revenueRate.Rate;
            existing.Attachments = revenueRate.Attachments;
            existing.Status = revenueRate.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, revenueRate.ActionBy);

            await _repository.UpdateAsync(existing);

            var newValuesJson = JsonSerializer.Serialize(new
            {
                existing.PropertyId,
                existing.CmdId,
                existing.BaseId,
                existing.ApplicableDate,
                existing.DeactiveDate,
                existing.Rate,
                existing.Attachments,
                existing.Status
            });
            await _auditLogService.LogAsync(new AuditLog
            {
                EntityName = "RevenueRate",
                EntityId = id,
                OldValuesJson = oldValuesJson,
                NewValuesJson = newValuesJson,
                ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext),
                Action = "API"
            });

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromBody] RevenueRateDeleteRequest? request = null)
        {
            var revenueRate = await _context.RevenueRates
                .FirstOrDefaultAsync(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));

            if (revenueRate == null)
            {
                return NotFound("Revenue rate not found.");
            }

            var oldValuesJson = JsonSerializer.Serialize(new
            {
                revenueRate.PropertyId,
                revenueRate.CmdId,
                revenueRate.BaseId,
                revenueRate.ApplicableDate,
                revenueRate.DeactiveDate,
                revenueRate.Rate,
                revenueRate.Attachments,
                revenueRate.Status,
                revenueRate.IsDeleted
            });

            var actionBy = request?.ActionBy;
            if (string.IsNullOrWhiteSpace(actionBy))
            {
                // If payload doesn't have ActionBy, preserve existing value
                var existingActionBy = await _context.RevenueRates
                    .AsNoTracking()
                    .Where(r => r.Id == id)
                    .Select(r => r.ActionBy)
                    .FirstOrDefaultAsync();
                actionBy = existingActionBy;
            }

            revenueRate.IsDeleted = true;
            revenueRate.Action = "DELETE";
            revenueRate.ActionDate = DateTime.UtcNow;
            revenueRate.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, actionBy);

            _context.RevenueRates.Update(revenueRate);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(new AuditLog
            {
                EntityName = "RevenueRate",
                EntityId = id,
                OldValuesJson = oldValuesJson,
                NewValuesJson = JsonSerializer.Serialize(new { IsDeleted = true, Action = "DELETE" }),
                ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext),
                Action = "API"
            });

            return NoContent();
        }
    }

    public class RevenueRateDeleteRequest
    {
        public string? ActionBy { get; set; }
    }
}

