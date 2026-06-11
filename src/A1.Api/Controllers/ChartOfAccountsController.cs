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
    public class ChartOfAccountsController : ControllerBase
    {
        private static readonly string[] CoaGroupOrder = { "Assets", "Liabilities", "Capital", "Suspense" };

        private readonly IGenericRepository<ChartOfAccount> _repository;
        private readonly ApplicationDbContext _context;

        public ChartOfAccountsController(
            IGenericRepository<ChartOfAccount> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? section = null)
        {
            var query = _context.ChartOfAccounts
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false);

            if (!string.IsNullOrWhiteSpace(section))
            {
                query = query.Where(x => x.SectionType == section);
            }

            var rows = SortCoaRows(await query.ToListAsync());

            return await OkWithAttachmentFlagsAsync(rows);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.ChartOfAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null) return NotFound();

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                new[] { row.Id },
                "ChartOfAccounts", "ChartOfAccount");

            return Ok(AttachmentFlagHelper.ToDictionaryWithAttachmentFlag(row, attachedIds.Contains(row.Id)));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ChartOfAccount entity)
        {
            if (entity == null) return BadRequest("Chart of account data is required.");

            var validationError = ValidateChartOfAccount(entity);
            if (validationError != null) return BadRequest(validationError);

            entity.IsDeleted = false;
            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "CREATE";
            entity.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.AddAsync(entity);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ChartOfAccount entity)
        {
            if (entity == null) return BadRequest("Chart of account data is required.");

            if (entity.Id == 0) entity.Id = id;
            else if (id != entity.Id) return BadRequest("ID mismatch.");

            var validationError = ValidateChartOfAccount(entity);
            if (validationError != null) return BadRequest(validationError);

            var existing = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null) return NotFound("Chart of account not found.");

            existing.AcctId = entity.AcctId;
            existing.AcctName = entity.AcctName;
            existing.GroupName = entity.GroupName;
            existing.SubGroup = entity.SubGroup;
            existing.ControlAccount = entity.ControlAccount;
            existing.SortOrder = entity.SortOrder;
            existing.SectionType = string.IsNullOrWhiteSpace(entity.SectionType)
                ? "(A) Balance Sheet"
                : entity.SectionType;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null) return NotFound();

            var section = existing.SectionType ?? "(A) Balance Sheet";
            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            await _repository.UpdateAsync(existing);
            await RenormalizeSortOrdersAsync(section);
            return NoContent();
        }

        [HttpPost("reorder")]
        public async Task<IActionResult> Reorder([FromBody] ChartOfAccountsReorderRequest request)
        {
            if (request == null) return BadRequest("Reorder payload is required.");
            if (request.Id <= 0) return BadRequest("A valid chart of account Id is required.");

            var direction = (request.Direction ?? string.Empty).Trim().ToLowerInvariant();
            if (direction != "up" && direction != "down")
                return BadRequest("Direction must be 'up' or 'down'.");

            var section = string.IsNullOrWhiteSpace(request.SectionType)
                ? "(A) Balance Sheet"
                : request.SectionType.Trim();

            var rows = await GetActiveRowsForSectionAsync(section, tracked: true);
            var movingRow = rows.FirstOrDefault(x => x.Id == request.Id);
            if (movingRow == null) return NotFound("Chart of account not found.");

            var groupRows = rows
                .Where(x => string.Equals(x.GroupName, movingRow.GroupName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToList();

            var index = groupRows.FindIndex(x => x.Id == request.Id);
            var targetIndex = direction == "up" ? index - 1 : index + 1;
            if (index < 0 || targetIndex < 0 || targetIndex >= groupRows.Count)
            {
                return await OkWithAttachmentFlagsAsync(await GetActiveRowsForSectionAsync(section));
            }

            var swappedRow = groupRows[index];
            groupRows.RemoveAt(index);
            groupRows.Insert(targetIndex, swappedRow);

            await ApplySequentialSortOrdersAsync(
                groupRows,
                ActionByHelper.GetActionByWithIp(User, HttpContext, null));

            var responseRows = await GetActiveRowsForSectionAsync(section);
            return await OkWithAttachmentFlagsAsync(responseRows);
        }

        [HttpPost("batch")]
        public async Task<IActionResult> BatchSave([FromBody] ChartOfAccountsBatchRequest request)
        {
            if (request == null) return BadRequest("Batch payload is required.");

            var actionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, null);
            var now = DateTime.UtcNow;

            foreach (var deleteId in request.DeleteIds ?? new List<int>())
            {
                var existing = await _context.ChartOfAccounts
                    .FirstOrDefaultAsync(x => x.Id == deleteId && (x.IsDeleted == null || x.IsDeleted == false));
                if (existing == null) continue;

                existing.IsDeleted = true;
                existing.ActionDate = now;
                existing.Action = "DELETE";
                existing.ActionBy = actionBy;
                await _repository.UpdateAsync(existing);
            }

            foreach (var entity in request.Creates ?? new List<ChartOfAccount>())
            {
                var validationError = ValidateChartOfAccount(entity);
                if (validationError != null) return BadRequest(validationError);

                entity.Id = 0;
                entity.IsDeleted = false;
                entity.ActionDate = now;
                entity.Action = "CREATE";
                entity.ActionBy = actionBy;
                await _repository.AddAsync(entity);
            }

            foreach (var entity in request.Updates ?? new List<ChartOfAccount>())
            {
                if (entity.Id <= 0) return BadRequest("Update records must include a valid Id.");

                var validationError = ValidateChartOfAccount(entity);
                if (validationError != null) return BadRequest(validationError);

                var existing = await _context.ChartOfAccounts
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && (x.IsDeleted == null || x.IsDeleted == false));

                if (existing == null) return NotFound($"Chart of account {entity.Id} not found.");

                existing.AcctId = entity.AcctId;
                existing.AcctName = entity.AcctName;
                existing.GroupName = entity.GroupName;
                existing.SubGroup = entity.SubGroup;
                existing.ControlAccount = entity.ControlAccount;
                existing.SortOrder = entity.SortOrder;
                existing.SectionType = string.IsNullOrWhiteSpace(entity.SectionType)
                    ? "(A) Balance Sheet"
                    : entity.SectionType;
                existing.ActionDate = now;
                existing.Action = "UPDATE";
                existing.ActionBy = actionBy;
                await _repository.UpdateAsync(existing);
            }

            var rows = SortCoaRows(await _context.ChartOfAccounts
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .ToListAsync());

            return await OkWithAttachmentFlagsAsync(rows);
        }

        private async Task<IActionResult> OkWithAttachmentFlagsAsync(List<ChartOfAccount> rows)
        {
            var sortedRows = SortCoaRows(rows);
            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                sortedRows.Select(x => x.Id),
                "ChartOfAccounts", "ChartOfAccount");

            var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(sortedRows, x => x.Id, attachedIds);
            return Ok(response);
        }

        private async Task<List<ChartOfAccount>> GetActiveRowsForSectionAsync(
            string section,
            bool tracked = false)
        {
            var query = _context.ChartOfAccounts
                .Where(x => (x.IsDeleted == null || x.IsDeleted == false) && x.SectionType == section);

            if (!tracked)
            {
                query = query.AsNoTracking();
            }

            return SortCoaRows(await query.ToListAsync());
        }

        private static int GetCoaGroupSortRank(string? groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName)) return CoaGroupOrder.Length;

            for (var i = 0; i < CoaGroupOrder.Length; i++)
            {
                if (string.Equals(CoaGroupOrder[i], groupName.Trim(), StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return CoaGroupOrder.Length;
        }

        private static List<ChartOfAccount> SortCoaRows(IEnumerable<ChartOfAccount> rows)
        {
            return rows
                .OrderBy(x => GetCoaGroupSortRank(x.GroupName))
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToList();
        }

        private async Task ApplySequentialSortOrdersAsync(List<ChartOfAccount> rows, string? actionBy)
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

        private async Task RenormalizeSortOrdersAsync(string section)
        {
            var rows = await GetActiveRowsForSectionAsync(section, tracked: true);
            if (rows.Count == 0) return;

            var actionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, null);
            foreach (var groupName in CoaGroupOrder)
            {
                var groupRows = rows
                    .Where(x => string.Equals(x.GroupName, groupName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .ToList();

                if (groupRows.Count == 0) continue;

                await ApplySequentialSortOrdersAsync(groupRows, actionBy);
            }
        }

        private static string? ValidateChartOfAccount(ChartOfAccount entity)
        {
            if (string.IsNullOrWhiteSpace(entity.AcctName))
                return "Acct Name is required.";

            if (string.IsNullOrWhiteSpace(entity.GroupName))
                return "Group is required.";

            var allowedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Assets", "Liabilities", "Capital", "Suspense"
            };

            if (!allowedGroups.Contains(entity.GroupName.Trim()))
                return "Group must be Assets, Liabilities, Capital, or Suspense.";

            return null;
        }
    }
}
