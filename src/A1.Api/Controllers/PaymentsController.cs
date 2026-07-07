using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private const string PaymentRecordType = "Payment";

        private readonly IGenericRepository<Receipt> _repository;
        private readonly ApplicationDbContext _context;

        public PaymentsController(
            IGenericRepository<Receipt> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await BuildPaymentQuery()
                .OrderByDescending(x => x.row.Date)
                .ThenByDescending(x => x.row.Id)
                .ToListAsync();

            return Ok(rows.Select(MapPaymentRow).ToList());
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await BuildPaymentQuery()
                .FirstOrDefaultAsync(x => x.row.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            return Ok(MapPaymentRow(item));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Receipt payment)
        {
            if (payment == null)
            {
                return BadRequest("Payment is required.");
            }

            var validation = ValidatePayment(payment);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            NormalizePayment(payment);
            payment.IsDeleted = false;
            payment.ActionDate = DateTime.UtcNow;
            payment.Action = "CREATE";
            payment.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, payment.ActionBy);

            await _repository.AddAsync(payment);
            return CreatedAtAction(nameof(GetById), new { id = payment.Id }, payment);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Receipt payment)
        {
            if (payment == null)
            {
                return BadRequest("Payment is required.");
            }

            if (payment.Id == 0)
            {
                payment.Id = id;
            }
            else if (payment.Id != id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.Receipts
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.RecordType == PaymentRecordType &&
                    (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Payment not found.");
            }

            var validation = ValidatePayment(payment);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            NormalizePayment(payment);
            existing.Date = payment.Date;
            existing.VrNo = payment.VrNo;
            existing.CashAndBankAccountId = payment.CashAndBankAccountId;
            existing.PaidFrom = payment.PaidFrom;
            existing.PayeeContactType = payment.PayeeContactType;
            existing.PayeePartyId = payment.PayeePartyId;
            existing.PayeePartyCode = payment.PayeePartyCode;
            existing.PayeeName = payment.PayeeName;
            existing.Description = payment.Description;
            existing.GrandTotal = payment.GrandTotal;
            existing.LinesJson = payment.LinesJson;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, payment.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.Receipts
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.RecordType == PaymentRecordType &&
                    (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Payment not found.");
            }

            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.Receipts.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private IQueryable<PaymentRow> BuildPaymentQuery()
        {
            return from row in _context.Receipts.AsNoTracking()
                   where row.RecordType == PaymentRecordType &&
                         (row.IsDeleted == null || row.IsDeleted == false)
                   join account in _context.CashAndBanks.Where(a => a.IsDeleted == null || a.IsDeleted == false)
                       on row.CashAndBankAccountId equals account.Id into accountGroup
                   from account in accountGroup.DefaultIfEmpty()
                   select new PaymentRow
                   {
                       row = row,
                       AcctId = account != null ? account.AcctId : string.Empty,
                       AccountName = account != null ? account.Name : string.Empty
                   };
        }

        private static Receipt MapPaymentRow(PaymentRow item)
        {
            item.row.ReceivedFromAccountDisplay = FormatAccountLabel(item.AcctId, item.AccountName);
            return item.row;
        }

        private static string FormatAccountLabel(string? acctId, string? name)
        {
            var id = (acctId ?? string.Empty).Trim();
            var accountName = (name ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(accountName))
            {
                return $"{id}-{accountName}";
            }

            return accountName.Length > 0 ? accountName : id;
        }

        private static void NormalizePayment(Receipt payment)
        {
            payment.RecordType = PaymentRecordType;
            payment.FinalizedByAhq = false;
            payment.Month = null;
            payment.ReferenceAutomatic = false;
            payment.Reference = null;
        }

        private static string? ValidatePayment(Receipt payment)
        {
            if (payment.Date == null)
            {
                return "Date is required.";
            }

            if (string.IsNullOrWhiteSpace(payment.VrNo))
            {
                return "Vr No is required.";
            }

            if (payment.CashAndBankAccountId == null || payment.CashAndBankAccountId <= 0)
            {
                return "Received from account is required.";
            }

            if (string.IsNullOrWhiteSpace(payment.PayeeName))
            {
                return "Paid To is required.";
            }

            if (payment.GrandTotal == null || payment.GrandTotal < 0)
            {
                return "Grand total is required.";
            }

            if (string.IsNullOrWhiteSpace(payment.LinesJson) || payment.LinesJson.Trim() == "[]")
            {
                return "At least one line item is required.";
            }

            return null;
        }

        private sealed class PaymentRow
        {
            public Receipt row { get; set; } = null!;
            public string AcctId { get; set; } = string.Empty;
            public string AccountName { get; set; } = string.Empty;
        }
    }
}
