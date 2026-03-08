using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Services;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace A1.Api.Controllers
{
    /// <summary>
    /// PropertyGroup Controller for managing property group records
    /// 
    /// GET /api/PropertyGroup - Get all property groups (only non-deleted)
    /// GET /api/PropertyGroup/{id} - Get property group by ID
    /// POST /api/PropertyGroup - Create a new property group with linked properties
    /// PUT /api/PropertyGroup/{id} - Update a property group
    /// DELETE /api/PropertyGroup/{id} - Soft delete a property group (sets IsDeleted = true)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PropertyGroupController : ControllerBase
    {
        private readonly IGenericRepository<PropertyGroup> _repository;
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLogService;

        public PropertyGroupController(IGenericRepository<PropertyGroup> repository, ApplicationDbContext context, IAuditLogService auditLogService)
        {
            _repository = repository;
            _context = context;
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// Helper method to get the current user for ActionBy tracking
        /// </summary>
        private string GetCurrentUser()
        {
            // Try to get from User claims first
            var userName = User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userName))
                return userName;

            // Try to get from claims
            var claim = User?.FindFirst(ClaimTypes.Name) ?? User?.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && !string.IsNullOrEmpty(claim.Value))
                return claim.Value;

            // Fallback to "System" if no user context available
            return "System";
        }

        /// <summary>
        /// GET: Get all property groups (only returns records where IsDeleted = 0 or null)
        /// Maps CmdId, BaseId, ClassId to their respective Name values
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 200) pageSize = 200; // safety cap for high-load scenarios

            var baseQuery = _context.PropertyGroups
                .AsNoTracking()
                .Where(pg => pg.IsDeleted == null || pg.IsDeleted == false);
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            // Join with related tables to get names efficiently using left joins
            var propertyGroups = await (from pg in baseQuery
                                       join cmd in _context.Commands.Where(c => c.IsDeleted == null || c.IsDeleted == false)
                                           on pg.CmdId equals cmd.Id into cmdGroup
                                       from cmd in cmdGroup.DefaultIfEmpty()
                                       join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                           on pg.BaseId equals b.Id into baseGroup
                                       from b in baseGroup.DefaultIfEmpty()
                                       join cls in _context.Classes.Where(c => c.IsDeleted == null || c.IsDeleted == false)
                                           on pg.ClassId equals cls.Id into classGroup
                                       from cls in classGroup.DefaultIfEmpty()
                                       orderby pg.Id descending
                                       select new
                                       {
                                           pg.Id,
                                           CmdId = pg.CmdId,
                                           CmdName = cmd != null ? cmd.Name : string.Empty,
                                           BaseId = pg.BaseId,
                                           BaseName = b != null ? b.Name : string.Empty,
                                           ClassId = pg.ClassId,
                                           ClassName = cls != null ? cls.Name : string.Empty,
                                           pg.GId,
                                           pg.UoM,
                                           pg.Area,
                                           pg.Rate,
                                           pg.Location,
                                           pg.Remarks,
                                           pg.Status,
                                           pg.ActionDate,
                                           pg.ActionBy,
                                           pg.Action,
                                           pg.IsDeleted
                                       })
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                propertyGroups.Select(x => x.Id),
                "PropertyGroup", "PropertyGroups");
            var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(propertyGroups, x => x.Id, attachedIds);
            return Ok(response);
        }

        /// <summary>
        /// GET: Get property group by ID (only returns if IsDeleted = 0 or null)
        /// Maps CmdId, BaseId, ClassId to their respective Name values
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var baseQuery = _context.PropertyGroups
                .AsNoTracking()
                .Where(pg => pg.Id == id && (pg.IsDeleted == null || pg.IsDeleted == false));
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            var propertyGroup = await (from pg in baseQuery
                                       join cmd in _context.Commands.Where(c => c.IsDeleted == null || c.IsDeleted == false)
                                           on pg.CmdId equals cmd.Id into cmdGroup
                                       from cmd in cmdGroup.DefaultIfEmpty()
                                       join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                           on pg.BaseId equals b.Id into baseGroup
                                       from b in baseGroup.DefaultIfEmpty()
                                       join cls in _context.Classes.Where(c => c.IsDeleted == null || c.IsDeleted == false)
                                           on pg.ClassId equals cls.Id into classGroup
                                       from cls in classGroup.DefaultIfEmpty()
                                       select new
                                       {
                                           pg.Id,
                                           CmdId = pg.CmdId,
                                           CmdName = cmd != null ? cmd.Name : string.Empty,
                                           BaseId = pg.BaseId,
                                           BaseName = b != null ? b.Name : string.Empty,
                                           ClassId = pg.ClassId,
                                           ClassName = cls != null ? cls.Name : string.Empty,
                                           pg.GId,
                                           pg.UoM,
                                           pg.Area,
                                           pg.Rate,
                                           pg.Location,
                                           pg.Remarks,
                                           pg.Status,
                                           pg.ActionDate,
                                           pg.ActionBy,
                                           pg.Action,
                                           pg.IsDeleted
                                       })
                .FirstOrDefaultAsync();

            if (propertyGroup == null)
            {
                return NotFound();
            }

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                new[] { propertyGroup.Id },
                "PropertyGroup", "PropertyGroups");
            return Ok(AttachmentFlagHelper.ToDictionaryWithAttachmentFlag(propertyGroup, attachedIds.Contains(propertyGroup.Id)));
        }

        /// <summary>
        /// GET: Get all property group linkings by GrpId (memory efficient - only returns Id, GrpId, PropId, GroupName)
        /// GET /api/PropertyGroup/ByGroup/{grpId} - Get all linked properties for a group
        /// </summary>
        [HttpGet("ByGroup/{grpId}")]
        public async Task<IActionResult> GetByGroupId(int grpId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 100)
        {
            if (grpId <= 0)
            {
                return BadRequest("Valid GrpId is required.");
            }

            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 100;
            if (pageSize > 500) pageSize = 500; // safety cap for high-load scenarios

            try
            {
                var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
                var accessibleGroupIds = DataAccessScopeHelper.ApplyScope(
                        _context.PropertyGroups.AsNoTracking().Where(g => g.Id == grpId),
                        scope)
                    .Select(g => g.Id);

                // Memory efficient query - projects only required fields using Select
                // Query filter for IsDeleted is automatically applied via BaseEntity
                var baseQuery = _context.PropertyGroupLinkings
                    .AsNoTracking()
                    .Where(l => l.GrpId == grpId && accessibleGroupIds.Contains(l.GrpId))
                    .Join(
                        _context.PropertyGroups.AsNoTracking(),
                        linking => linking.GrpId,
                        group => group.Id,
                        (linking, group) => new { linking, group }
                    )
                    .GroupJoin(
                        _context.RentalProperties.AsNoTracking(),
                        lg => lg.linking.PropId,
                        prop => prop.Id,
                        (lg, props) => new { lg.linking, lg.group, props }
                    )
                    .SelectMany(
                        x => x.props.DefaultIfEmpty(),
                        (x, prop) => new
                        {
                            Id = x.linking.Id,
                            GrpId = x.linking.GrpId,
                            PropId = x.linking.PropId,
                            GroupName = x.group.GId ?? string.Empty,
                            PropertyName = prop != null ? prop.PId ?? string.Empty : string.Empty
                        }
                    );

                var totalCount = await baseQuery.CountAsync();
                Response.Headers["X-Total-Count"] = totalCount.ToString();
                Response.Headers["X-Page-Number"] = pageNumber.ToString();
                Response.Headers["X-Page-Size"] = pageSize.ToString();

                var linkings = await baseQuery
                    .OrderByDescending(x => x.Id)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                    _context,
                    linkings.Select(x => x.Id),
                    "PropertyGroupLinking", "PropertyGroupLinkings");
                var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(linkings, x => x.Id, attachedIds);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving property group linkings.", error = ex.Message });
            }
        }

        /// <summary>
        /// GET: Returns rental properties for a given cmdId/baseId that are not linked in any active group
        /// and also not part of any active contract.
        /// Route: /api/PropertyGroup/NotGroupedProperties?cmdId=1&baseId=1
        /// </summary>
        [HttpGet("NotGroupedProperties")]
        public async Task<IActionResult> NotGroupedProperties([FromQuery] int cmdId, [FromQuery] int baseId)
        {
            if (cmdId <= 0 || baseId <= 0)
            {
                return BadRequest("Valid cmdId and baseId are required.");
            }

            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            if (!scope.IsAhq)
            {
                if (string.Equals(scope.AccessLevel, "base", StringComparison.OrdinalIgnoreCase))
                {
                    if (!scope.BaseId.HasValue || scope.BaseId.Value != baseId)
                    {
                        return Forbid();
                    }
                }
                else if (string.Equals(scope.AccessLevel, "command", StringComparison.OrdinalIgnoreCase))
                {
                    var baseMismatch = scope.BaseId.HasValue
                        ? scope.BaseId.Value != baseId
                        : !scope.AllowedBaseIds.Contains(baseId);
                    var cmdMismatch = !scope.CmdId.HasValue || scope.CmdId.Value != cmdId;
                    if (baseMismatch || cmdMismatch)
                    {
                        return Forbid();
                    }
                }
            }

            var today = DateTime.UtcNow.Date;

            // Active groups: not deleted and enabled.
            var activeGroupIds = _context.PropertyGroups
                .AsNoTracking()
                .Where(g => (g.IsDeleted == null || g.IsDeleted == false) && g.Status == true)
                .Select(g => g.Id);

            // Properties already linked in active groups.
            var linkedPropertyIds = _context.PropertyGroupLinkings
                .AsNoTracking()
                .Where(l => (l.IsDeleted == null || l.IsDeleted == false) && l.Status == true && activeGroupIds.Contains(l.GrpId))
                .Select(l => l.PropId);

            // Active contracts based on status + date window.
            var activeContractGroupIds = _context.Contracts
                .AsNoTracking()
                .Where(c => (c.IsDeleted == null || c.IsDeleted == false)
                            && c.Status
                            && c.ContractStartDate.Date <= today
                            && c.ContractEndDate.Date >= today)
                .Select(c => c.GrpId);

            // Properties consumed by active contracts via their groups.
            var contractedPropertyIds = _context.PropertyGroupLinkings
                .AsNoTracking()
                .Where(l => (l.IsDeleted == null || l.IsDeleted == false) && activeContractGroupIds.Contains(l.GrpId))
                .Select(l => l.PropId);

            // Combine excluded property ids.
            var excludedPropertyIds = linkedPropertyIds.Union(contractedPropertyIds);

            // Step 1: Return only available properties for requested cmd/base.
            var availableProperties = await (from r in _context.RentalProperties.AsNoTracking()
                                             where (r.IsDeleted == null || r.IsDeleted == false)
                                                   && r.Status == true
                                                   && r.CmdId == cmdId
                                                   && r.BaseId == baseId
                                                   && !excludedPropertyIds.Contains(r.Id)
                                             join cls in _context.Classes.AsNoTracking()
                                                 on r.ClassId equals cls.Id into clsGroup
                                             from cls in clsGroup.DefaultIfEmpty()
                                             join pt in _context.PropertyTypes.AsNoTracking()
                                                 on r.PropertyType equals pt.Id into ptGroup
                                             from pt in ptGroup.DefaultIfEmpty()
                                             orderby r.Id descending
                                             select new
                                             {
                                                 r.Id,
                                                 r.CmdId,
                                                 r.BaseId,
                                                 r.ClassId,
                                                 ClassName = cls != null ? cls.Name : string.Empty,
                                                 r.PId,
                                                 r.UoM,
                                                 r.Area,
                                                 r.Location,
                                                 r.Remarks,
                                                 r.PropertyType,
                                                 PropertyTypeName = pt != null ? pt.Name : string.Empty
                                             }).ToListAsync();

            if (availableProperties.Count == 0)
            {
                return Ok(availableProperties);
            }

            // Step 2: Batch fetch latest active/non-deleted revenue rates for returned property ids.
            var propertyIds = availableProperties.Select(p => p.Id).ToList();
            var rateRows = await _context.RevenueRates
                .AsNoTracking()
                .Where(rr => propertyIds.Contains(rr.PropertyId)
                             && (rr.IsDeleted == null || rr.IsDeleted == false)
                             && rr.Status == true
                             && rr.Rate != null)
                .OrderByDescending(rr => rr.ApplicableDate)
                .ThenByDescending(rr => rr.Id)
                .Select(rr => new { rr.PropertyId, rr.Rate, rr.ApplicableDate })
                .ToListAsync();

            var rateByPropertyId = new Dictionary<int, decimal?>();
            var applicableDateByPropertyId = new Dictionary<int, DateTime?>();
            foreach (var row in rateRows)
            {
                if (!rateByPropertyId.ContainsKey(row.PropertyId))
                {
                    rateByPropertyId[row.PropertyId] = row.Rate;
                    applicableDateByPropertyId[row.PropertyId] = row.ApplicableDate;
                }
            }

            var response = availableProperties.Select(p => new
            {
                p.Id,
                p.CmdId,
                p.BaseId,
                p.ClassId,
                p.ClassName,
                p.PId,
                p.UoM,
                p.Area,
                p.Location,
                p.Remarks,
                p.PropertyType,
                p.PropertyTypeName,
                Rate = rateByPropertyId.TryGetValue(p.Id, out var rate) ? rate : null,
                ApplicableDate = applicableDateByPropertyId.TryGetValue(p.Id, out var applicableDate) ? applicableDate : null
            }).ToList();

            var attachedPropertyIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                response.Select(x => x.Id),
                "RentalProperty", "RentalProperties");
            var responseWithAttachment = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(response, x => x.Id, attachedPropertyIds);
            return Ok(responseWithAttachment);
        }

        /// <summary>
        /// POST: Create a new property group with linked properties
        /// Request body should include PropertyGroup fields at root and PropertyGroupLinkings as array of PropIds
        /// Example: { "cmdId": 1, "baseId": 1, "classId": 4, "gId": "testGrp", "PropertyGroupLinkings": [1, 3], "area": 13.45, ... }
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PropertyGroupCreateRequest request)
        {
            if (request == null)
            {
                return BadRequest("PropertyGroup data is required.");
            }

            // Use transaction for atomicity and memory efficiency
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Create PropertyGroup entity from request
                var propertyGroup = new PropertyGroup
                {
                    CmdId = request.CmdId,
                    BaseId = request.BaseId,
                    ClassId = request.ClassId,
                    GId = request.GId,
                    UoM = request.UoM,
                    Area = request.Area,
                    Rate = request.Rate,
                    Location = request.Location,
                    Remarks = request.Remarks,
                    Status = request.Status,
                    IsDeleted = false,
                    ActionBy = request.ActionBy
                };

                // Save PropertyGroup first
                await _repository.AddAsync(propertyGroup);
                
                // Get the generated Id (repository saves and generates Id)
                var grpId = propertyGroup.Id;

                // Save PropertyGroupLinkings if provided
                // PropertyGroupLinkings is an array of PropIds (integers)
                if (request.PropertyGroupLinkings != null && request.PropertyGroupLinkings.Count > 0)
                {
                    // Fetch property details (area) for all PropIds in one query
                    var validPropIds = request.PropertyGroupLinkings.Where(p => p > 0).ToList();
                    var properties = await _context.RentalProperties
                        .AsNoTracking()
                        .Where(p => validPropIds.Contains(p.Id))
                        .Select(p => new { p.Id, p.Area })
                        .ToDictionaryAsync(p => p.Id);

                    // Fetch latest active rates for properties
                    var propertyIds = validPropIds.ToList();
                    var latestRates = await _context.RevenueRates
                        .AsNoTracking()
                        .Where(rr => propertyIds.Contains(rr.PropertyId)
                                     && (rr.IsDeleted == null || rr.IsDeleted == false)
                                     && rr.Status == true
                                     && rr.Rate != null)
                        .OrderByDescending(rr => rr.ApplicableDate)
                        .ThenByDescending(rr => rr.Id)
                        .GroupBy(rr => rr.PropertyId)
                        .Select(g => new { PropertyId = g.Key, Rate = g.First().Rate })
                        .ToDictionaryAsync(r => r.PropertyId);

                    // Create linking records efficiently in batch with individual property areas
                    var linkingRecords = new List<PropertyGroupLinking>(validPropIds.Count);
                    var now = DateTime.UtcNow;
                    decimal totalArea = 0;
                    decimal totalRate = 0;
                    
                    foreach (var propId in validPropIds)
                    {
                        if (!properties.TryGetValue(propId, out var prop))
                            continue;

                        var propArea = prop.Area ?? 0;
                        var propRate = latestRates.TryGetValue(propId, out var rate) ? (rate.Rate ?? 0) : 0;

                        linkingRecords.Add(new PropertyGroupLinking
                        {
                            GrpId = grpId,
                            PropId = propId,
                            Area = propArea, // Store individual property area
                            Status = true,
                            IsDeleted = false,
                            ActionDate = now,
                            Action = "CREATE",
                            ActionBy = request.ActionBy
                        });

                        totalArea += propArea;
                        totalRate += propRate;
                    }

                    // Batch insert for memory efficiency
                    if (linkingRecords.Count > 0)
                    {
                        await _context.PropertyGroupLinkings.AddRangeAsync(linkingRecords);
                        await _context.SaveChangesAsync();

                        // Update PropertyGroup with calculated totals
                        propertyGroup.Area = totalArea;
                        propertyGroup.Rate = totalRate;
                        propertyGroup.ActionBy = request.ActionBy;
                        await _context.SaveChangesAsync();
                    }
                }

                // Commit transaction
                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetById), new { id = propertyGroup.Id }, new
                {
                    propertyGroup = propertyGroup,
                    linkedPropertiesCount = request.PropertyGroupLinkings?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                // Rollback on error
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "An error occurred while creating the property group.", error = ex.Message });
            }
        }

        /// <summary>
        /// POST: Create a single property group linking
        /// Stores individual property area and updates PropertyGroup totals
        /// </summary>
        [HttpPost("Linking")]
        public async Task<IActionResult> CreatePropertyGroupLinking([FromBody] PropertyGroupLinking request)
        {
            if (request == null)
            {
                return BadRequest("PropertyGroupLinking data is required.");
            }

            if (request.GrpId <= 0 || request.PropId <= 0)
            {
                return BadRequest("Valid GrpId and PropId are required.");
            }

            // Fetch property's area from RentalProperty
            var property = await _context.RentalProperties
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PropId);

            if (property == null)
            {
                return BadRequest("Property not found.");
            }

            // Fetch latest active rate for the property
            var propertyRate = await _context.RevenueRates
                .AsNoTracking()
                .Where(rr => rr.PropertyId == request.PropId
                             && (rr.IsDeleted == null || rr.IsDeleted == false)
                             && rr.Status == true
                             && rr.Rate != null)
                .OrderByDescending(rr => rr.ApplicableDate)
                .ThenByDescending(rr => rr.Id)
                .Select(rr => rr.Rate)
                .FirstOrDefaultAsync() ?? 0;

            var propertyArea = property.Area ?? 0;

            var now = DateTime.UtcNow;

            // Set individual property area in linking record
            request.Area = propertyArea;
            request.Status ??= true;
            request.IsDeleted = false;
            request.ActionDate = now;
            request.Action = "CREATE";
            // ActionBy comes from payload

            await _context.PropertyGroupLinkings.AddAsync(request);
            await _context.SaveChangesAsync();

            // Update PropertyGroup totals by adding this property's area and rate
            var propertyGroup = await _context.PropertyGroups
                .FirstOrDefaultAsync(pg => pg.Id == request.GrpId && (pg.IsDeleted == null || pg.IsDeleted == false));

            if (propertyGroup != null)
            {
                propertyGroup.Area = (propertyGroup.Area ?? 0) + propertyArea;
                propertyGroup.Rate = (propertyGroup.Rate ?? 0) + propertyRate;
                propertyGroup.ActionDate = DateTime.UtcNow;
                propertyGroup.Action = "UPDATE";
                propertyGroup.ActionBy = request.ActionBy;
                _context.PropertyGroups.Update(propertyGroup);
                await _context.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetByGroupId), new { grpId = request.GrpId }, request);
        }

        /// <summary>
        /// PUT: Update an existing property group
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PropertyGroup propertyGroup)
        {
            if (propertyGroup == null)
            {
                return BadRequest("PropertyGroup data is required.");
            }

            // If propertyGroup.Id is not set (0), use the route parameter id
            if (propertyGroup.Id == 0)
            {
                propertyGroup.Id = id;
            }
            else if (id != propertyGroup.Id)
            {
                return BadRequest("ID mismatch.");
            }

            // Check if property group exists and is not deleted
            var existingPropertyGroup = await _context.PropertyGroups
                .FirstOrDefaultAsync(pg => pg.Id == id && (pg.IsDeleted == null || pg.IsDeleted == false));

            if (existingPropertyGroup == null)
            {
                return NotFound("PropertyGroup not found.");
            }

            var oldValuesJson = JsonSerializer.Serialize(new
            {
                existingPropertyGroup.CmdId,
                existingPropertyGroup.BaseId,
                existingPropertyGroup.ClassId,
                existingPropertyGroup.GId,
                existingPropertyGroup.UoM,
                existingPropertyGroup.Area,
                existingPropertyGroup.Rate,
                existingPropertyGroup.Location,
                existingPropertyGroup.Remarks,
                existingPropertyGroup.Status
            });

            // Update properties efficiently
            existingPropertyGroup.CmdId = propertyGroup.CmdId;
            existingPropertyGroup.BaseId = propertyGroup.BaseId;
            existingPropertyGroup.ClassId = propertyGroup.ClassId;
            existingPropertyGroup.GId = propertyGroup.GId;
            existingPropertyGroup.UoM = propertyGroup.UoM;
            existingPropertyGroup.Area = propertyGroup.Area;
            existingPropertyGroup.Rate = propertyGroup.Rate;
            existingPropertyGroup.Location = propertyGroup.Location;
            existingPropertyGroup.Remarks = propertyGroup.Remarks;
            existingPropertyGroup.Status = propertyGroup.Status;
            existingPropertyGroup.ActionDate = DateTime.UtcNow;
            existingPropertyGroup.Action = "UPDATE";
            existingPropertyGroup.ActionBy = propertyGroup.ActionBy;

            await _repository.UpdateAsync(existingPropertyGroup);

            var newValuesJson = JsonSerializer.Serialize(new
            {
                existingPropertyGroup.CmdId,
                existingPropertyGroup.BaseId,
                existingPropertyGroup.ClassId,
                existingPropertyGroup.GId,
                existingPropertyGroup.UoM,
                existingPropertyGroup.Area,
                existingPropertyGroup.Rate,
                existingPropertyGroup.Location,
                existingPropertyGroup.Remarks,
                existingPropertyGroup.Status
            });
            await _auditLogService.LogAsync(new AuditLog
            {
                EntityName = "PropertyGroup",
                EntityId = id,
                OldValuesJson = oldValuesJson,
                NewValuesJson = newValuesJson,
                ActionBy = GetCurrentUser(),
                Action = "API"
            });

            return NoContent();
        }

        /// <summary>
        /// DELETE: Soft delete a property group (sets IsDeleted = true)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromBody] PropertyGroupDeleteRequest? request = null)
        {
            var propertyGroup = await _context.PropertyGroups
                .FirstOrDefaultAsync(pg => pg.Id == id && (pg.IsDeleted == null || pg.IsDeleted == false));

            if (propertyGroup == null)
            {
                return NotFound("PropertyGroup not found.");
            }

            var actionBy = request?.ActionBy;
            if (string.IsNullOrWhiteSpace(actionBy))
            {
                // If payload doesn't have ActionBy, preserve existing value
                var existingActionBy = await _context.PropertyGroups
                    .AsNoTracking()
                    .Where(pg => pg.Id == id)
                    .Select(pg => pg.ActionBy)
                    .FirstOrDefaultAsync();
                actionBy = existingActionBy;
            }

            // Soft delete group - set Status = false and IsDeleted = true
            propertyGroup.Status = false;
            propertyGroup.IsDeleted = true;
            propertyGroup.Action = "DELETE";
            propertyGroup.ActionDate = DateTime.UtcNow;
            propertyGroup.ActionBy = actionBy;

            // Also deactivate and soft delete related active linkings
            var activeLinkings = await _context.PropertyGroupLinkings
                .Where(l => l.GrpId == id && (l.IsDeleted == null || l.IsDeleted == false))
                .ToListAsync();

            if (activeLinkings.Count > 0)
            {
                foreach (var linking in activeLinkings)
                {
                    linking.Status = false;
                    linking.IsDeleted = true;
                    linking.Action = "DELETE";
                    linking.ActionDate = DateTime.UtcNow;
                    linking.ActionBy = actionBy;
                }

                _context.PropertyGroupLinkings.UpdateRange(activeLinkings);
            }

            _context.PropertyGroups.Update(propertyGroup);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// DELETE: Soft delete a property group linking by Id (sets IsDeleted = true)
        /// Also deducts the property's area and rate from PropertyGroup totals to maintain consistency
        /// DELETE /api/PropertyGroup/Linking/{id} - Delete a property group linking
        /// </summary>
        [HttpDelete("Linking/{id}")]
        public async Task<IActionResult> DeleteLinking(int id, [FromBody] PropertyGroupLinkingDeleteRequest? request = null)
        {
            if (id <= 0)
            {
                return BadRequest("Valid linking ID is required.");
            }

            var linking = await _context.PropertyGroupLinkings
                .FirstOrDefaultAsync(l => l.Id == id && (l.IsDeleted == null || l.IsDeleted == false));

            if (linking == null)
            {
                return NotFound("PropertyGroupLinking not found.");
            }

            var actionBy = request?.ActionBy;
            if (string.IsNullOrWhiteSpace(actionBy))
            {
                // If payload doesn't have ActionBy, preserve existing value
                var existingActionBy = await _context.PropertyGroupLinkings
                    .AsNoTracking()
                    .Where(l => l.Id == id)
                    .Select(l => l.ActionBy)
                    .FirstOrDefaultAsync();
                actionBy = existingActionBy;
            }

            // Get the property's area from linking record (individual property area)
            var propertyArea = linking.Area ?? 0;

            // Get the property's latest active rate from RevenueRate
            var propertyRate = await _context.RevenueRates
                .AsNoTracking()
                .Where(rr => rr.PropertyId == linking.PropId
                             && (rr.IsDeleted == null || rr.IsDeleted == false)
                             && rr.Status == true
                             && rr.Rate != null)
                .OrderByDescending(rr => rr.ApplicableDate)
                .ThenByDescending(rr => rr.Id)
                .Select(rr => rr.Rate)
                .FirstOrDefaultAsync() ?? 0;

            // Get the PropertyGroup to update totals
            var propertyGroup = await _context.PropertyGroups
                .FirstOrDefaultAsync(pg => pg.Id == linking.GrpId && (pg.IsDeleted == null || pg.IsDeleted == false));

            if (propertyGroup != null)
            {
                // Deduct area and rate from PropertyGroup totals
                propertyGroup.Area = (propertyGroup.Area ?? 0) - propertyArea;
                propertyGroup.Rate = (propertyGroup.Rate ?? 0) - propertyRate;
                
                // Ensure values don't go negative
                if (propertyGroup.Area < 0) propertyGroup.Area = 0;
                if (propertyGroup.Rate < 0) propertyGroup.Rate = 0;

                propertyGroup.ActionDate = DateTime.UtcNow;
                propertyGroup.Action = "UPDATE";
                propertyGroup.ActionBy = actionBy;
                _context.PropertyGroups.Update(propertyGroup);
            }

            // Soft delete - set IsDeleted = true
            linking.IsDeleted = true;
            linking.Status = false;
            linking.Action = "DELETE";
            linking.ActionDate = DateTime.UtcNow;
            linking.ActionBy = actionBy;

            _context.PropertyGroupLinkings.Update(linking);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    /// <summary>
    /// Request DTO for creating PropertyGroup with linked properties
    /// Accepts PropertyGroup fields at root level and PropertyGroupLinkings as array of PropIds
    /// </summary>
    public class PropertyGroupCreateRequest : PropertyGroup
    {
        // PropertyGroupLinkings as array of PropIds (integers)
        public List<int>? PropertyGroupLinkings { get; set; }
    }

    /// <summary>
    /// Request DTO for deleting PropertyGroup
    /// </summary>
    public class PropertyGroupDeleteRequest
    {
        public string? ActionBy { get; set; }
    }

    /// <summary>
    /// Request DTO for deleting PropertyGroupLinking
    /// </summary>
    public class PropertyGroupLinkingDeleteRequest
    {
        public string? ActionBy { get; set; }
    }
}

