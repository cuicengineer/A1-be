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
            return NoContent();
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
