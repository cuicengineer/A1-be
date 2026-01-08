using A1.Api.Models;
using A1.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

        public PropertyGroupController(IGenericRepository<PropertyGroup> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
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

            return Ok(propertyGroups);
        }

        /// <summary>
        /// GET: Get property group by ID (only returns if IsDeleted = 0 or null)
        /// Maps CmdId, BaseId, ClassId to their respective Name values
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var propertyGroup = await (from pg in _context.PropertyGroups
                                      .AsNoTracking()
                                      .Where(pg => pg.Id == id && (pg.IsDeleted == null || pg.IsDeleted == false))
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

            return Ok(propertyGroup);
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
                // Memory efficient query - projects only required fields using Select
                // Query filter for IsDeleted is automatically applied via BaseEntity
                var baseQuery = _context.PropertyGroupLinkings
                    .AsNoTracking()
                    .Where(l => l.GrpId == grpId)
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

                return Ok(linkings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving property group linkings.", error = ex.Message });
            }
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
                // Get current user for ActionBy
                var currentUser = GetCurrentUser();

                // Create PropertyGroup entity from request
                var propertyGroup = new PropertyGroup
                {
                    CmdId = request.CmdId,
                    BaseId = request.BaseId,
                    ClassId = request.ClassId,
                    GId = request.GId,
                    UoM = request.UoM,
                    Area = request.Area,
                    Location = request.Location,
                    Remarks = request.Remarks,
                    Status = request.Status,
                    IsDeleted = false,
                    ActionBy = currentUser
                };

                // Save PropertyGroup first
                await _repository.AddAsync(propertyGroup);
                
                // Get the generated Id (repository saves and generates Id)
                var grpId = propertyGroup.Id;

                // Save PropertyGroupLinkings if provided
                // PropertyGroupLinkings is an array of PropIds (integers)
                if (request.PropertyGroupLinkings != null && request.PropertyGroupLinkings.Count > 0)
                {
                    // Create linking records efficiently in batch
                    var linkingRecords = new List<PropertyGroupLinking>(request.PropertyGroupLinkings.Count);
                    var now = DateTime.UtcNow;
                    
                    foreach (var propId in request.PropertyGroupLinkings)
                    {
                        // Skip invalid records
                        if (propId <= 0)
                            continue;

                        linkingRecords.Add(new PropertyGroupLinking
                        {
                            GrpId = grpId,
                            PropId = propId,
                            Area = request.Area, // Use area from PropertyGroup root level
                            Status = true,
                            IsDeleted = false,
                            ActionDate = now,
                            Action = "CREATE",
                            ActionBy = currentUser
                        });
                    }

                    // Batch insert for memory efficiency
                    if (linkingRecords.Count > 0)
                    {
                        await _context.PropertyGroupLinkings.AddRangeAsync(linkingRecords);
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

            var currentUser = GetCurrentUser();
            var now = DateTime.UtcNow;

            request.Status ??= true;
            request.IsDeleted = false;
            request.ActionDate = now;
            request.Action = "CREATE";
            request.ActionBy = currentUser;

            await _context.PropertyGroupLinkings.AddAsync(request);
            await _context.SaveChangesAsync();

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

            // Get current user for ActionBy
            var currentUser = GetCurrentUser();

            // Update properties efficiently
            existingPropertyGroup.CmdId = propertyGroup.CmdId;
            existingPropertyGroup.BaseId = propertyGroup.BaseId;
            existingPropertyGroup.ClassId = propertyGroup.ClassId;
            existingPropertyGroup.GId = propertyGroup.GId;
            existingPropertyGroup.UoM = propertyGroup.UoM;
            existingPropertyGroup.Area = propertyGroup.Area;
            existingPropertyGroup.Location = propertyGroup.Location;
            existingPropertyGroup.Remarks = propertyGroup.Remarks;
            existingPropertyGroup.Status = propertyGroup.Status;
            existingPropertyGroup.ActionDate = DateTime.UtcNow;
            existingPropertyGroup.Action = "UPDATE";
            existingPropertyGroup.ActionBy = currentUser;

            await _repository.UpdateAsync(existingPropertyGroup);
            return NoContent();
        }

        /// <summary>
        /// DELETE: Soft delete a property group (sets IsDeleted = true)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var propertyGroup = await _context.PropertyGroups
                .FirstOrDefaultAsync(pg => pg.Id == id && (pg.IsDeleted == null || pg.IsDeleted == false));

            if (propertyGroup == null)
            {
                return NotFound("PropertyGroup not found.");
            }

            // Get current user for ActionBy
            var currentUser = GetCurrentUser();

            // Soft delete - set IsDeleted = true
            propertyGroup.IsDeleted = true;
            propertyGroup.Action = "DELETE";
            propertyGroup.ActionDate = DateTime.UtcNow;
            propertyGroup.ActionBy = currentUser;

            _context.PropertyGroups.Update(propertyGroup);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// DELETE: Soft delete a property group linking by Id (sets IsDeleted = true)
        /// DELETE /api/PropertyGroup/Linking/{id} - Delete a property group linking
        /// </summary>
        [HttpDelete("Linking/{id}")]
        public async Task<IActionResult> DeleteLinking(int id)
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

            // Get current user for ActionBy
            var currentUser = GetCurrentUser();

            // Soft delete - set IsDeleted = true
            linking.IsDeleted = true;
            linking.Action = "DELETE";
            linking.ActionDate = DateTime.UtcNow;
            linking.ActionBy = currentUser;

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
}

