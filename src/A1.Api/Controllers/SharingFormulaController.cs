using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace A1.Api.Controllers
{
    /// <summary>
    /// SharingFormula Controller for managing sharing formula records
    /// 
    /// GET /api/SharingFormula - Get all sharing formulas (only non-deleted)
    /// GET /api/SharingFormula/{id} - Get sharing formula by ID
    /// POST /api/SharingFormula - Create a new sharing formula (accepts single object or list of objects)
    /// PUT /api/SharingFormula/{id} - Update a sharing formula
    /// DELETE /api/SharingFormula/{id} - Soft delete a sharing formula (sets IsDeleted = true)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SharingFormulaController : ControllerBase
    {
        private readonly IGenericRepository<SharingFormula> _repository;
        private readonly ApplicationDbContext _context;

        public SharingFormulaController(IGenericRepository<SharingFormula> repository, ApplicationDbContext context)
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
        /// GET: Get all sharing formulas (only returns records where IsDeleted = 0 or null)
        /// Maps CmdId, BaseId, ClassId to their respective Name values
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 200) pageSize = 200; // safety cap for high-load scenarios

            var baseQuery = _context.SharingFormulas
                .AsNoTracking()
                .Where(sf => sf.IsDeleted == null || sf.IsDeleted == false);
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            // Join with related tables to get names efficiently using left joins
            var sharingFormulas = await (from sf in baseQuery
                                       join cmd in _context.Commands.Where(c => c.IsDeleted == null || c.IsDeleted == false)
                                           on sf.CmdId equals cmd.Id into cmdGroup
                                       from cmd in cmdGroup.DefaultIfEmpty()
                                       join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                           on sf.BaseId equals b.Id into baseGroup
                                       from b in baseGroup.DefaultIfEmpty()
                                       join cls in _context.Classes.Where(c => c.IsDeleted == null || c.IsDeleted == false)
                                           on sf.ClassId equals cls.Id into classGroup
                                       from cls in classGroup.DefaultIfEmpty()
                                       orderby sf.Id descending
                                       select new
                                       {
                                           sf.Id,
                                           CmdId = sf.CmdId,
                                           CmdName = cmd != null ? cmd.Name : string.Empty,
                                           BaseId = sf.BaseId,
                                           BaseName = b != null ? b.Name : string.Empty,
                                           ClassId = sf.ClassId,
                                           ClassName = cls != null ? cls.Name : string.Empty,
                                           sf.Type,
                                           sf.ApplicableDate,
                                           sf.AHQRate,
                                           sf.RACRate,
                                           sf.BaseRate,
                                           sf.Description,
                                           sf.Attachments,
                                           sf.Status,
                                           sf.ActionDate,
                                           sf.ActionBy,
                                           sf.Action,
                                           sf.IsDeleted
                                       })
                                       .Skip((pageNumber - 1) * pageSize)
                                       .Take(pageSize)
                                       .ToListAsync();

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                sharingFormulas.Select(x => x.Id),
                "SharingFormula", "SharingFormulas");
            var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(sharingFormulas, x => x.Id, attachedIds);
            return Ok(response);
        }

        /// <summary>
        /// GET: Get sharing formula by ID (only returns records where IsDeleted = 0 or null)
        /// Maps CmdId, BaseId, ClassId to their respective Name values
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var baseQuery = _context.SharingFormulas
                .AsNoTracking()
                .Where(sf => sf.Id == id && (sf.IsDeleted == null || sf.IsDeleted == false));
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            var sharingFormula = await (from sf in baseQuery
                                      join cmd in _context.Commands.Where(c => c.IsDeleted == null || c.IsDeleted == false)
                                          on sf.CmdId equals cmd.Id into cmdGroup
                                      from cmd in cmdGroup.DefaultIfEmpty()
                                      join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                          on sf.BaseId equals b.Id into baseGroup
                                      from b in baseGroup.DefaultIfEmpty()
                                      join cls in _context.Classes.Where(c => c.IsDeleted == null || c.IsDeleted == false)
                                          on sf.ClassId equals cls.Id into classGroup
                                      from cls in classGroup.DefaultIfEmpty()
                                      select new
                                      {
                                          sf.Id,
                                          CmdId = sf.CmdId,
                                          CmdName = cmd != null ? cmd.Name : string.Empty,
                                          BaseId = sf.BaseId,
                                          BaseName = b != null ? b.Name : string.Empty,
                                          ClassId = sf.ClassId,
                                          ClassName = cls != null ? cls.Name : string.Empty,
                                          sf.Type,
                                          sf.ApplicableDate,
                                          sf.AHQRate,
                                          sf.RACRate,
                                          sf.BaseRate,
                                          sf.Description,
                                          sf.Attachments,
                                          sf.Status,
                                          sf.ActionDate,
                                          sf.ActionBy,
                                          sf.Action,
                                          sf.IsDeleted
                                      })
                                      .FirstOrDefaultAsync();

            if (sharingFormula == null) return NotFound();
            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                new[] { sharingFormula.Id },
                "SharingFormula", "SharingFormulas");
            return Ok(AttachmentFlagHelper.ToDictionaryWithAttachmentFlag(sharingFormula, attachedIds.Contains(sharingFormula.Id)));
        }

        /// <summary>
        /// POST: Create a new sharing formula or list of sharing formulas
        /// Accepts either a single object or an array of objects
        /// Example single: { "classId": 1, "type": 1, "cmdId": 1, "baseId": 1, "applicableDate": "2025-01-01", "ahqRate": 10.5, "racRate": 20.5, "baseRate": 30.5, ... }
        /// Example list: [{ "classId": 1, ... }, { "classId": 2, ... }]
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] JsonElement jsonData)
        {
            if (jsonData.ValueKind == JsonValueKind.Null || jsonData.ValueKind == JsonValueKind.Undefined)
            {
                return BadRequest("Data is required.");
            }

            var currentUser = GetCurrentUser();
            var now = DateTime.UtcNow;

            try
            {
                // Check if it's an array or single object
                if (jsonData.ValueKind == JsonValueKind.Array)
                {
                    // Handle list of objects
                    var items = JsonSerializer.Deserialize<List<SharingFormula>>(jsonData.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (items == null || items.Count == 0)
                    {
                        return BadRequest("At least one item is required.");
                    }

                    // Set common properties for all items
                    foreach (var item in items)
                    {
                        item.IsDeleted = false;
                        item.ActionDate = now;
                        item.Action = "CREATE";
                        item.ActionBy = currentUser;
                    }

                    // Batch insert for memory efficiency
                    await _context.SharingFormulas.AddRangeAsync(items);
                    await _context.SaveChangesAsync();

                    return CreatedAtAction(nameof(GetAll), new { }, new
                    {
                        message = $"Successfully created {items.Count} sharing formula(s).",
                        count = items.Count,
                        items = items.Select(i => new { i.Id, i.ClassId, i.Type, i.CmdId, i.BaseId })
                    });
                }
                else
                {
                    // Handle single object
                    var item = JsonSerializer.Deserialize<SharingFormula>(jsonData.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (item == null)
                    {
                        return BadRequest("Invalid data format.");
                    }

                    item.IsDeleted = false;
                    item.ActionDate = now;
                    item.Action = "CREATE";
                    item.ActionBy = currentUser;

                    await _repository.AddAsync(item);
                    return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
                }
            }
            catch (JsonException ex)
            {
                return BadRequest(new { message = "Invalid JSON format.", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the sharing formula(s).", error = ex.Message });
            }
        }

        /// <summary>
        /// PUT: Update a sharing formula
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] SharingFormula item)
        {
            if (item == null) return BadRequest("Data is required.");

            if (item.Id == 0)
                item.Id = id;
            else if (item.Id != id)
                return BadRequest("ID mismatch.");

            var existing = await _context.SharingFormulas
                .FirstOrDefaultAsync(sf => sf.Id == id && (sf.IsDeleted == null || sf.IsDeleted == false));
            if (existing == null) return NotFound();

            var currentUser = GetCurrentUser();

            existing.ClassId = item.ClassId;
            existing.Type = item.Type;
            existing.CmdId = item.CmdId;
            existing.BaseId = item.BaseId;
            existing.ApplicableDate = item.ApplicableDate;
            existing.AHQRate = item.AHQRate;
            existing.RACRate = item.RACRate;
            existing.BaseRate = item.BaseRate;
            existing.Description = item.Description;
            existing.Attachments = item.Attachments;
            existing.Status = item.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = currentUser;

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        /// <summary>
        /// DELETE: Soft delete a sharing formula (sets IsDeleted = true)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.SharingFormulas
                .FirstOrDefaultAsync(sf => sf.Id == id && (sf.IsDeleted == null || sf.IsDeleted == false));
            if (existing == null) return NotFound();

            var currentUser = GetCurrentUser();

            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = currentUser;

            await _repository.UpdateAsync(existing);
            return NoContent();
        }
    }
}

