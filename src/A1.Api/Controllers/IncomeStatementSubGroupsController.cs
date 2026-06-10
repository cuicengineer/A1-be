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
    public class IncomeStatementSubGroupsController : ControllerBase
    {
        private readonly IGenericRepository<IncomeStatementSubGroup> _repository;
        private readonly ApplicationDbContext _context;

        public IncomeStatementSubGroupsController(
            IGenericRepository<IncomeStatementSubGroup> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetByGroup([FromQuery] string? groupName = null)
        {
            var query = _context.IncomeStatementSubGroups
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
        public async Task<IActionResult> Create([FromBody] IncomeStatementSubGroup entity)
        {
            if (entity == null) return BadRequest("Sub-group data is required.");

            if (string.IsNullOrWhiteSpace(entity.GroupName))
                return BadRequest("Group is required.");

            if (string.IsNullOrWhiteSpace(entity.SubGroupName))
                return BadRequest("Sub-group name is required.");

            var groupName = entity.GroupName.Trim();
            var subGroupName = entity.SubGroupName.Trim();

            var allowedGroups = new[] { "Revenue", "Expenses" };
            if (!allowedGroups.Contains(groupName, StringComparer.OrdinalIgnoreCase))
                return BadRequest("Group must be Revenue or Expenses.");

            var duplicate = await _context.IncomeStatementSubGroups
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
    }
}
