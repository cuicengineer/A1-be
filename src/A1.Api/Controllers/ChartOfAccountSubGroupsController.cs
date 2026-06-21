using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChartOfAccountSubGroupsController : ControllerBase
    {
        private readonly IGenericRepository<ChartOfAccountSubGroup> _repository;
        private readonly ApplicationDbContext _context;

        public ChartOfAccountSubGroupsController(
            IGenericRepository<ChartOfAccountSubGroup> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetByGroup([FromQuery] string? groupName = null)
        {
            var query = _context.ChartOfAccountSubGroups
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false);

            if (!string.IsNullOrWhiteSpace(groupName))
            {
                query = query.Where(x => x.GroupName == groupName);
            }

            var rows = await query
                .OrderBy(x => x.SubGroupName)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return Ok(rows);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ChartOfAccountSubGroup entity)
        {
            if (entity == null) return BadRequest("Sub-group data is required.");

            if (string.IsNullOrWhiteSpace(entity.GroupName))
                return BadRequest("Group is required.");

            if (string.IsNullOrWhiteSpace(entity.SubGroupName))
                return BadRequest("Sub-group name is required.");

            var groupName = entity.GroupName.Trim();
            var subGroupName = entity.SubGroupName.Trim();

            var duplicate = await _context.ChartOfAccountSubGroups
                .AsNoTracking()
                .AnyAsync(x =>
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    x.GroupName == groupName &&
                    x.SubGroupName == subGroupName);

            if (duplicate)
                return Conflict("This sub-group already exists for the selected group.");

            entity.GroupName = groupName;
            entity.SubGroupName = subGroupName;
            entity.IsDeleted = false;
            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "CREATE";
            entity.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.AddAsync(entity);
            return CreatedAtAction(nameof(GetByGroup), new { groupName = entity.GroupName }, entity);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ChartOfAccountSubGroup entity)
        {
            if (entity == null) return BadRequest("Sub-group data is required.");

            if (entity.Id == 0) entity.Id = id;
            else if (id != entity.Id) return BadRequest("ID mismatch.");

            if (string.IsNullOrWhiteSpace(entity.GroupName))
                return BadRequest("Group is required.");

            if (string.IsNullOrWhiteSpace(entity.SubGroupName))
                return BadRequest("Sub-group name is required.");

            var existing = await _context.ChartOfAccountSubGroups
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null) return NotFound("Sub-group not found.");

            var groupName = entity.GroupName.Trim();
            var subGroupName = entity.SubGroupName.Trim();
            var previousGroupName = existing.GroupName;
            var previousSubGroupName = existing.SubGroupName;

            var duplicate = await _context.ChartOfAccountSubGroups
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id != id &&
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    x.GroupName == groupName &&
                    x.SubGroupName == subGroupName);

            if (duplicate)
                return Conflict("This sub-group already exists for the selected group.");

            existing.GroupName = groupName;
            existing.SubGroupName = subGroupName;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.UpdateAsync(existing);

            if (!string.Equals(previousSubGroupName, subGroupName, StringComparison.Ordinal)
                || !string.Equals(previousGroupName, groupName, StringComparison.Ordinal))
            {
                var coaRows = await _context.ChartOfAccounts
                    .Where(x =>
                        (x.IsDeleted == null || x.IsDeleted == false) &&
                        x.GroupName == previousGroupName &&
                        x.SubGroup == previousSubGroupName)
                    .ToListAsync();

                foreach (var coaRow in coaRows)
                {
                    coaRow.GroupName = groupName;
                    coaRow.SubGroup = subGroupName;
                    coaRow.ActionDate = DateTime.UtcNow;
                    coaRow.Action = "UPDATE";
                    coaRow.ActionBy = existing.ActionBy;
                }

                if (coaRows.Count > 0)
                {
                    await _context.SaveChangesAsync();
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromBody] ChartOfAccountSubGroupDeleteRequest? request = null)
        {
            var existing = await _context.ChartOfAccountSubGroups
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null) return NotFound("Sub-group not found.");

            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, request?.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }
    }

    public class ChartOfAccountSubGroupDeleteRequest
    {
        public string? ActionBy { get; set; }
    }
}
