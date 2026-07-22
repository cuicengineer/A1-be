using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 0)
        {
            pageNumber = PaginationHelper.NormalizePageNumber(pageNumber);

            var baseQuery = _context.RentalProperties
                .AsNoTracking()
                .Where(r => r.IsDeleted == null || r.IsDeleted == false);
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = PaginationHelper.FormatPageSizeHeader(pageSize, totalCount);

            var payloadQuery =
                from r in baseQuery
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
                                 };

            var payload = await PaginationHelper.ApplyPaging(payloadQuery, pageNumber, pageSize)
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

            var pId = (rentalProperty.PId ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(pId))
            {
                var duplicateExists = await _context.RentalProperties
                    .AsNoTracking()
                    .AnyAsync(r =>
                        r.PId == pId
                        && (r.IsDeleted == null || r.IsDeleted == false));

                if (duplicateExists)
                {
                    return Conflict(
                        $"Property ID \"{pId}\" already exists. Delete the existing property first to reuse this Property No.");
                }

                // Soft-deleted rows may still hold the same PId under a unique index — free them so reuse works.
                var softDeletedSamePId = await _context.RentalProperties
                    .Where(r => r.PId == pId && r.IsDeleted == true)
                    .ToListAsync();
                if (softDeletedSamePId.Count > 0)
                {
                    foreach (var oldProperty in softDeletedSamePId)
                    {
                        if (oldProperty.PId != null
                            && oldProperty.PId.IndexOf("#DEL#", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            oldProperty.PId = $"{oldProperty.PId.Trim()}#DEL#{oldProperty.Id}";
                        }
                    }
                    await _context.SaveChangesAsync();
                }
            }

            rentalProperty.PId = pId;
            rentalProperty.IsDeleted = false;
            rentalProperty.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, rentalProperty.ActionBy);
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

            var isLinkedToNonDeletedGroup = await _context.PropertyGroupLinkings
                .AsNoTracking()
                .AnyAsync(l => l.PropId == id
                    && (l.IsDeleted == null || l.IsDeleted == false)
                    && _context.PropertyGroups.Any(g => g.Id == l.GrpId && (g.IsDeleted == null || g.IsDeleted == false)));

            if (isLinkedToNonDeletedGroup && !IsLoginSuperuser(User))
            {
                return Conflict("Cannot update this rental property because it is linked to a property group that is not deleted.");
            }

            var updatedPId = (rentalProperty.PId ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(updatedPId))
            {
                var duplicateExists = await _context.RentalProperties
                    .AsNoTracking()
                    .AnyAsync(r =>
                        r.Id != id
                        && r.PId == updatedPId
                        && (r.IsDeleted == null || r.IsDeleted == false));

                if (duplicateExists)
                {
                    return Conflict(
                        $"Property ID \"{updatedPId}\" already exists. Delete the existing property first to reuse this Property No.");
                }

                var softDeletedSamePId = await _context.RentalProperties
                    .Where(r => r.Id != id && r.PId == updatedPId && r.IsDeleted == true)
                    .ToListAsync();
                if (softDeletedSamePId.Count > 0)
                {
                    foreach (var oldProperty in softDeletedSamePId)
                    {
                        if (oldProperty.PId != null
                            && oldProperty.PId.IndexOf("#DEL#", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            oldProperty.PId = $"{oldProperty.PId.Trim()}#DEL#{oldProperty.Id}";
                        }
                    }
                    await _context.SaveChangesAsync();
                }
            }

            existing.CmdId = rentalProperty.CmdId;
            existing.BaseId = rentalProperty.BaseId;
            existing.ClassId = rentalProperty.ClassId;
            existing.PId = updatedPId;
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

            // Keep property-grouping snapshots in sync with live Area / UoM / Location.
            await SyncPropertyGroupFieldsFromRentalPropertyAsync(existing);

            return NoContent();
        }

        /// <summary>
        /// Updates PropertyGroupLinking.Area and recalculates each linked PropertyGroup's
        /// Area, Location, and UoM from current rental-property values.
        /// </summary>
        private async Task SyncPropertyGroupFieldsFromRentalPropertyAsync(RentalProperty property)
        {
            var linkings = await _context.PropertyGroupLinkings
                .Where(l => l.PropId == property.Id
                    && (l.IsDeleted == null || l.IsDeleted == false))
                .ToListAsync();

            if (linkings.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var actionBy = property.ActionBy;

            foreach (var linking in linkings)
            {
                linking.Area = property.Area;
                linking.ActionDate = now;
                linking.Action = "UPDATE";
                linking.ActionBy = actionBy;
            }

            var groupIds = linkings.Select(l => l.GrpId).Distinct().ToList();
            var groups = await _context.PropertyGroups
                .Where(g => groupIds.Contains(g.Id) && (g.IsDeleted == null || g.IsDeleted == false))
                .ToListAsync();

            if (groups.Count == 0)
            {
                await _context.SaveChangesAsync();
                return;
            }

            var allLinkingsForGroups = await _context.PropertyGroupLinkings
                .AsNoTracking()
                .Where(l => groupIds.Contains(l.GrpId)
                    && (l.IsDeleted == null || l.IsDeleted == false)
                    && (l.Status == null || l.Status == true))
                .Select(l => new { l.GrpId, l.PropId })
                .ToListAsync();

            var propIds = allLinkingsForGroups.Select(l => l.PropId).Distinct().ToList();
            var propRows = await _context.RentalProperties
                .AsNoTracking()
                .Where(p => propIds.Contains(p.Id) && (p.IsDeleted == null || p.IsDeleted == false))
                .Select(p => new { p.Id, p.Area, p.Location, p.UoM })
                .ToListAsync();
            var propsById = propRows.ToDictionary(
                p => p.Id,
                p => new PropSnapshot(p.Id, p.Area, p.Location, p.UoM));

            // Prefer the just-updated in-memory values for this property.
            propsById[property.Id] = new PropSnapshot(property.Id, property.Area, property.Location, property.UoM);

            foreach (var group in groups)
            {
                var linkedPropIds = allLinkingsForGroups
                    .Where(l => l.GrpId == group.Id)
                    .Select(l => l.PropId)
                    .Distinct()
                    .ToList();

                var linkedProps = linkedPropIds
                    .Where(propsById.ContainsKey)
                    .Select(pid => propsById[pid])
                    .ToList();

                group.Area = linkedProps.Sum(p => p.Area ?? 0m);
                group.Location = string.Join(", ", linkedProps
                    .Select(p => (p.Location ?? string.Empty).Trim())
                    .Where(loc => loc.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase));
                var uoms = linkedProps
                    .Select(p => (p.UoM ?? string.Empty).Trim())
                    .Where(u => u.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (uoms.Count > 0)
                {
                    group.UoM = string.Join(", ", uoms);
                }

                group.ActionDate = now;
                group.Action = "UPDATE";
                group.ActionBy = actionBy;
            }

            await _context.SaveChangesAsync();
        }

        private static bool IsLoginSuperuser(ClaimsPrincipal user)
        {
            var loginName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity?.Name;
            return string.Equals(loginName?.Trim(), "superuser", StringComparison.OrdinalIgnoreCase);
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

            var isLinkedToNonDeletedGroup = await _context.PropertyGroupLinkings
                .AsNoTracking()
                .AnyAsync(l => l.PropId == id
                    && (l.IsDeleted == null || l.IsDeleted == false)
                    && _context.PropertyGroups.Any(g => g.Id == l.GrpId && (g.IsDeleted == null || g.IsDeleted == false)));

            if (isLinkedToNonDeletedGroup && !IsLoginSuperuser(User))
            {
                return Conflict("Cannot delete this rental property because it is linked to a property group that is not deleted.");
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

            // Soft delete and release PId so the same Property No / Property ID can be recreated.
            rental.IsDeleted = true;
            if (!string.IsNullOrWhiteSpace(rental.PId)
                && rental.PId.IndexOf("#DEL#", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rental.PId = $"{rental.PId.Trim()}#DEL#{rental.Id}";
            }
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

    file sealed record PropSnapshot(int Id, decimal? Area, string? Location, string? UoM);
}

