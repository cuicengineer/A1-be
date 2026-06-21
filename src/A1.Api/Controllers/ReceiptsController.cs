using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReceiptsController : ControllerBase
    {
        private static readonly Regex MonthYearPattern = new(
            @"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)/\d{4}$",
            RegexOptions.Compiled);

        private readonly IGenericRepository<Receipt> _repository;
        private readonly ApplicationDbContext _context;

        public ReceiptsController(
            IGenericRepository<Receipt> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.Receipts
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.Receipts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null)
            {
                return NotFound();
            }

            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Receipt receipt)
        {
            if (receipt == null)
            {
                return BadRequest("Receipt is required.");
            }

            var validation = ValidateReceipt(receipt);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            if (receipt.FinalizedByAhq == true && !await CanFinalizeReceiptByAhqAsync())
            {
                return Forbid();
            }

            if (receipt.FinalizedByAhq != true)
            {
                receipt.FinalizedByAhq = false;
            }

            receipt.IsDeleted = false;
            receipt.ActionDate = DateTime.UtcNow;
            receipt.Action = "CREATE";
            receipt.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, receipt.ActionBy);

            await _repository.AddAsync(receipt);
            return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, receipt);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Receipt receipt)
        {
            if (receipt == null)
            {
                return BadRequest("Receipt is required.");
            }

            if (receipt.Id == 0)
            {
                receipt.Id = id;
            }
            else if (receipt.Id != id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.Receipts
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Receipt not found.");
            }

            var validation = ValidateReceipt(receipt);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            if (IsFinalizedByAhqChanging(existing, receipt) && !await CanFinalizeReceiptByAhqAsync())
            {
                return Forbid();
            }

            existing.Date = receipt.Date;
            existing.Month = receipt.Month;
            existing.ReferenceAutomatic = receipt.ReferenceAutomatic;
            existing.Reference = receipt.Reference;
            existing.PaidFrom = receipt.PaidFrom;
            existing.PayeeContactType = receipt.PayeeContactType;
            existing.PayeeName = receipt.PayeeName;
            existing.Description = receipt.Description;
            existing.GrandTotal = receipt.GrandTotal;
            existing.LinesJson = receipt.LinesJson;
            existing.AttachmentsJson = receipt.AttachmentsJson;
            if (await CanFinalizeReceiptByAhqAsync())
            {
                existing.FinalizedByAhq = receipt.FinalizedByAhq == true;
            }
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, receipt.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.Receipts
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Receipt not found.");
            }

            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.Receipts.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static string? ValidateReceipt(Receipt receipt)
        {
            if (receipt.Date == null) return "Date is required.";
            if (string.IsNullOrWhiteSpace(receipt.Month) || !MonthYearPattern.IsMatch(receipt.Month.Trim()))
            {
                return "Month is required in MMM/YYYY format.";
            }

            if (string.IsNullOrWhiteSpace(receipt.PaidFrom)) return "Paid from is required.";
            if (string.IsNullOrWhiteSpace(receipt.PayeeName)) return "Payee is required.";
            if (receipt.GrandTotal == null || receipt.GrandTotal < 0) return "Grand total is required.";
            return null;
        }

        private static bool IsFinalizedByAhqChanging(Receipt existing, Receipt source)
        {
            return (existing.FinalizedByAhq == true) != (source.FinalizedByAhq == true);
        }

        private static bool IsLoginSuperuser(ClaimsPrincipal user)
        {
            var loginName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity?.Name;
            return string.Equals(loginName?.Trim(), "superuser", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CanFinalizeReceiptByAhqAsync()
        {
            if (IsLoginSuperuser(User))
            {
                return true;
            }

            var category = User.FindFirstValue("category");
            return DataAccessScopeHelper.IsSupervisorCategory(category)
                && await DataAccessScopeHelper.IsAhqUserAsync(User, _context);
        }
    }
}
