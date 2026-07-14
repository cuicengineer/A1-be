using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JournalEntriesController : ControllerBase
    {
        private readonly IGenericRepository<JournalEntry> _repository;
        private readonly ApplicationDbContext _context;

        public JournalEntriesController(
            IGenericRepository<JournalEntry> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        public class JournalEntryLockStatusRequest
        {
            public List<int> Ids { get; set; } = new();
            public bool IsLock { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.JournalEntries
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderByDescending(x => x.EntryDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync();

            await JournalEntryLineHelper.AttachLinesAsync(_context, rows);
            return Ok(rows);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.JournalEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null)
            {
                return NotFound();
            }

            await JournalEntryLineHelper.AttachLinesAsync(_context, new[] { row });
            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] JournalEntry item)
        {
            if (item == null)
            {
                return BadRequest("Journal entry data is required.");
            }

            var incomingLines = JournalEntryLineHelper.ResolveIncomingLines(item);
            var validationError = ValidateJournalEntry(item, incomingLines);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            NormalizeJournalEntry(item);
            item.IsLock = false;
            item.IsDeleted = false;
            item.ActionDate = DateTime.UtcNow;
            item.Action = "CREATE";
            item.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);
            item.LinesJson = null;

            await _repository.AddAsync(item);

            await JournalEntryLineHelper.ReplaceLinesAsync(
                _context,
                item,
                incomingLines,
                item.ActionBy);
            await _context.SaveChangesAsync();

            item.Lines = incomingLines;
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("lock-status")]
        public async Task<IActionResult> UpdateLockStatus([FromBody] JournalEntryLockStatusRequest request)
        {
            if (!await CanManageJournalEntryLockAsync())
            {
                return BadRequest("Only superuser or AHQ supervisor can change journal entry lock status.");
            }

            var ids = (request?.Ids ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (ids.Count == 0)
            {
                return BadRequest("At least one journal entry id is required.");
            }

            var rows = await _context.JournalEntries
                .Where(x => ids.Contains(x.Id) && (x.IsDeleted == null || x.IsDeleted == false))
                .ToListAsync();

            if (rows.Count == 0)
            {
                return NotFound("No matching journal entries found.");
            }

            var actionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, null);
            var now = DateTime.UtcNow;
            var nextLock = request?.IsLock == true;

            foreach (var row in rows)
            {
                row.IsLock = nextLock;
                row.ActionDate = now;
                row.Action = "UPDATE";
                row.ActionBy = actionBy;
            }

            await _context.SaveChangesAsync();
            return Ok(new { updated = rows.Count, isLock = nextLock });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] JournalEntry item)
        {
            if (item == null)
            {
                return BadRequest("Journal entry data is required.");
            }

            if (item.Id == 0)
            {
                item.Id = id;
            }
            else if (item.Id != id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.JournalEntries
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Journal entry not found.");
            }

            if (existing.IsLock)
            {
                return BadRequest("Locked journal entries cannot be edited.");
            }

            var incomingLines = JournalEntryLineHelper.ResolveIncomingLines(item);
            var validationError = ValidateJournalEntry(item, incomingLines);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            NormalizeJournalEntry(item);
            existing.EntryDate = item.EntryDate;
            existing.VrNo = item.VrNo;
            existing.Description = item.Description;
            existing.TotalDebit = item.TotalDebit;
            existing.TotalCredit = item.TotalCredit;
            existing.LinesJson = null;
            existing.AttachmentsJson = item.AttachmentsJson;
            // Lock status is managed only via lock-status endpoint.
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await JournalEntryLineHelper.ReplaceLinesAsync(
                _context,
                existing,
                incomingLines,
                existing.ActionBy);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.JournalEntries
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Journal entry not found.");
            }

            if (existing.IsLock)
            {
                return BadRequest("Locked journal entries cannot be deleted.");
            }

            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            await JournalEntryLineHelper.SoftDeleteLinesAsync(_context, id, existing.ActionBy);
            _context.JournalEntries.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static string? ValidateJournalEntry(JournalEntry item, IReadOnlyList<JournalEntryLine> lines)
        {
            if (item.EntryDate == default)
            {
                return "Date is required.";
            }

            if (string.IsNullOrWhiteSpace(item.VrNo))
            {
                return "Vr No is required.";
            }

            if (item.TotalDebit < 0 || item.TotalCredit < 0)
            {
                return "Debit and credit totals cannot be negative.";
            }

            if (Math.Round(item.TotalDebit, 2) != Math.Round(item.TotalCredit, 2))
            {
                return "Total debit must equal total credit.";
            }

            if (item.TotalDebit <= 0)
            {
                return "At least one line with debit or credit is required.";
            }

            if (lines == null || lines.Count == 0)
            {
                return "At least one line item is required.";
            }

            var lineDebit = Math.Round(lines.Sum(x => x.Debit), 2);
            var lineCredit = Math.Round(lines.Sum(x => x.Credit), 2);
            if (lineDebit != Math.Round(item.TotalDebit, 2) || lineCredit != Math.Round(item.TotalCredit, 2))
            {
                return "Line totals must match header totals.";
            }

            return null;
        }

        private static void NormalizeJournalEntry(JournalEntry item)
        {
            item.VrNo = (item.VrNo ?? string.Empty).Trim();
            item.Description = string.IsNullOrWhiteSpace(item.Description)
                ? null
                : item.Description.Trim();
            if (!string.IsNullOrEmpty(item.Description) && item.Description.Length > 50)
            {
                item.Description = item.Description[..50];
            }

            item.TotalDebit = Math.Round(item.TotalDebit, 2);
            item.TotalCredit = Math.Round(item.TotalCredit, 2);
        }

        private static bool IsLoginSuperuser(ClaimsPrincipal user)
        {
            var loginName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity?.Name;
            return string.Equals(loginName?.Trim(), "superuser", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CanManageJournalEntryLockAsync()
        {
            if (IsLoginSuperuser(User))
            {
                return true;
            }

            return await DataAccessScopeHelper.IsAhqSupervisorAsync(User, _context);
        }
    }
}
