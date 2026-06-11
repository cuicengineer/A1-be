using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IncomeStatementsController : ControllerBase
    {
        private readonly IGenericRepository<IncomeStatement> _repository;
        private readonly ApplicationDbContext _context;

        public IncomeStatementsController(
            IGenericRepository<IncomeStatement> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.IncomeStatements
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return await OkWithAttachmentFlagsAsync(rows);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.IncomeStatements
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null) return NotFound();

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                new[] { row.Id },
                "IncomeStatements", "IncomeStatement");

            return Ok(AttachmentFlagHelper.ToDictionaryWithAttachmentFlag(row, attachedIds.Contains(row.Id)));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] IncomeStatement entity)
        {
            if (entity == null) return BadRequest("Income statement data is required.");

            var validationError = ValidateIncomeStatement(entity);
            if (validationError != null) return BadRequest(validationError);

            entity.IsDeleted = false;
            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "CREATE";
            entity.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.AddAsync(entity);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] IncomeStatement entity)
        {
            if (entity == null) return BadRequest("Income statement data is required.");

            if (entity.Id == 0) entity.Id = id;
            else if (id != entity.Id) return BadRequest("ID mismatch.");

            var validationError = ValidateIncomeStatement(entity);
            if (validationError != null) return BadRequest(validationError);

            var existing = await _context.IncomeStatements
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null) return NotFound("Income statement record not found.");

            existing.AcctId = entity.AcctId;
            existing.AcctName = entity.AcctName;
            existing.GroupName = entity.GroupName;
            existing.SubGroup = entity.SubGroup;
            existing.SortOrder = entity.SortOrder;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.IncomeStatements
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null) return NotFound();

            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            await _repository.UpdateAsync(existing);
            await RenormalizeSortOrdersAsync();
            return NoContent();
        }

        [HttpPost("reorder")]
        public async Task<IActionResult> Reorder([FromBody] IncomeStatementsReorderRequest request)
        {
            if (request == null) return BadRequest("Reorder payload is required.");
            if (request.Id <= 0) return BadRequest("A valid income statement Id is required.");

            var direction = (request.Direction ?? string.Empty).Trim().ToLowerInvariant();
            if (direction != "up" && direction != "down")
                return BadRequest("Direction must be 'up' or 'down'.");

            var rows = await GetActiveRowsAsync(tracked: true);
            var index = rows.FindIndex(x => x.Id == request.Id);
            if (index < 0) return NotFound("Income statement record not found.");

            var targetIndex = direction == "up" ? index - 1 : index + 1;
            if (targetIndex < 0 || targetIndex >= rows.Count)
            {
                return await OkWithAttachmentFlagsAsync(await GetActiveRowsAsync());
            }

            var movingRow = rows[index];
            rows.RemoveAt(index);
            rows.Insert(targetIndex, movingRow);

            await ApplySequentialSortOrdersAsync(
                rows,
                ActionByHelper.GetActionByWithIp(User, HttpContext, null));

            var responseRows = await GetActiveRowsAsync();
            return await OkWithAttachmentFlagsAsync(responseRows);
        }

        private async Task<List<IncomeStatement>> GetActiveRowsAsync(bool tracked = false)
        {
            var query = _context.IncomeStatements
                .Where(x => x.IsDeleted == null || x.IsDeleted == false);

            if (!tracked)
            {
                query = query.AsNoTracking();
            }

            return await query
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync();
        }

        private async Task ApplySequentialSortOrdersAsync(List<IncomeStatement> rows, string? actionBy)
        {
            var now = DateTime.UtcNow;
            for (var i = 0; i < rows.Count; i++)
            {
                var newOrder = i + 1;
                if (rows[i].SortOrder == newOrder) continue;

                rows[i].SortOrder = newOrder;
                rows[i].ActionDate = now;
                rows[i].Action = "UPDATE";
                rows[i].ActionBy = actionBy;
            }

            await _context.SaveChangesAsync();
        }

        private async Task RenormalizeSortOrdersAsync()
        {
            var rows = await GetActiveRowsAsync(tracked: true);
            if (rows.Count == 0) return;

            await ApplySequentialSortOrdersAsync(
                rows,
                ActionByHelper.GetActionByWithIp(User, HttpContext, null));
        }

        private async Task<IActionResult> OkWithAttachmentFlagsAsync(List<IncomeStatement> rows)
        {
            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                rows.Select(x => x.Id),
                "IncomeStatements", "IncomeStatement");

            var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(rows, x => x.Id, attachedIds);
            return Ok(response);
        }

        private static string? ValidateIncomeStatement(IncomeStatement entity)
        {
            if (string.IsNullOrWhiteSpace(entity.AcctName))
                return "Acct Name is required.";

            if (string.IsNullOrWhiteSpace(entity.GroupName))
                return "Group is required.";

            var allowedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Revenue", "Expenses"
            };

            if (!allowedGroups.Contains(entity.GroupName.Trim()))
                return "Group must be Revenue or Expenses.";

            return null;
        }
    }
}
