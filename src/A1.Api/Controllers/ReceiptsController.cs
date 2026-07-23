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
                .Where(x =>
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    (x.RecordType == null || x.RecordType == "" || x.RecordType == "Receipt"))
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            await ReceiptLineHelper.AttachLinesAsync(_context, rows);
            return Ok(rows);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.Receipts
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    (x.RecordType == null || x.RecordType == "" || x.RecordType == "Receipt"));

            if (row == null)
            {
                return NotFound();
            }

            await ReceiptLineHelper.AttachLinesAsync(_context, new[] { row });
            return Ok(row);
        }

        /// <summary>
        /// Receipt lines linked to an invoice with TIN-TRN / TIN-FTN assigned.
        /// Same filter as agreement-prov-invoice Received / PDF receipt rows.
        /// Route: GET /api/Receipts/lines-by-invoice?invoiceNo=&amp;contractNo=
        /// </summary>
        [HttpGet("lines-by-invoice")]
        public async Task<IActionResult> GetTinLinesByInvoice(
            [FromQuery] string invoiceNo,
            [FromQuery] string? contractNo = null,
            CancellationToken cancellationToken = default)
        {
            var targetInvoice = (invoiceNo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(targetInvoice))
            {
                return BadRequest("invoiceNo is required.");
            }

            var targetContract = (contractNo ?? string.Empty).Trim();

            var tinLines = await (
                from rl in _context.ReceiptLines.AsNoTracking()
                join r in _context.Receipts.AsNoTracking() on rl.ReceiptId equals r.Id
                where (rl.IsDeleted == null || rl.IsDeleted == false)
                      && (r.IsDeleted == null || r.IsDeleted == false)
                      && (r.RecordType == null || r.RecordType == "" || r.RecordType == "Receipt")
                      && (
                          (rl.TinTrn != null && rl.TinTrn.Trim() != "")
                          || (rl.TinFtn != null && rl.TinFtn.Trim() != "")
                      )
                select rl
            ).ToListAsync(cancellationToken);

            var collectionEntryIds = tinLines
                .Select(x => (x.CollectionEntryId ?? string.Empty).Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id) && int.TryParse(id, out _))
                .Select(int.Parse)
                .Distinct()
                .ToList();

            var collectionInvoiceById = new Dictionary<int, string>();
            if (collectionEntryIds.Count > 0)
            {
                var entries = await _context.CollectionEntries
                    .AsNoTracking()
                    .Where(x =>
                        collectionEntryIds.Contains(x.Id)
                        && (x.IsDeleted == null || x.IsDeleted == false))
                    .Select(x => new { x.Id, x.InvoiceNo })
                    .ToListAsync(cancellationToken);

                foreach (var entry in entries)
                {
                    var inv = (entry.InvoiceNo ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(inv))
                    {
                        collectionInvoiceById[entry.Id] = inv;
                    }
                }
            }

            static string ResolveInvoiceNo(
                string? invoiceNoValue,
                string? invoiceKey,
                string? collectionEntryId,
                IReadOnlyDictionary<int, string> collectionInvoiceById)
            {
                var direct = (invoiceNoValue ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(direct))
                {
                    return direct;
                }

                var entryIdText = (collectionEntryId ?? string.Empty).Trim();
                if (int.TryParse(entryIdText, out var entryId)
                    && collectionInvoiceById.TryGetValue(entryId, out var fromEntry)
                    && !string.IsNullOrWhiteSpace(fromEntry))
                {
                    return fromEntry.Trim();
                }

                var key = (invoiceKey ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    return string.Empty;
                }

                // Collection-entry option keys are not invoice numbers.
                if (key.StartsWith("ce:", StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith("collectionentry:", StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith("entry:", StringComparison.OrdinalIgnoreCase))
                {
                    var cePart = key.Contains(':') ? key[(key.IndexOf(':') + 1)..].Trim() : "";
                    if (int.TryParse(cePart, out var ceId)
                        && collectionInvoiceById.TryGetValue(ceId, out var fromKey)
                        && !string.IsNullOrWhiteSpace(fromKey))
                    {
                        return fromKey.Trim();
                    }

                    return string.Empty;
                }

                var parts = key.Split('|');
                return parts.Length > 1 ? parts[^1].Trim() : key;
            }

            bool MatchesInvoice(ReceiptLine line)
            {
                var lineInvoice = ResolveInvoiceNo(
                    line.InvoiceNo,
                    line.InvoiceKey,
                    line.CollectionEntryId,
                    collectionInvoiceById);
                if (!string.IsNullOrWhiteSpace(lineInvoice)
                    && string.Equals(lineInvoice, targetInvoice, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var key = (line.InvoiceKey ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    return false;
                }

                if (string.Equals(key, targetInvoice, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(targetContract)
                    && string.Equals(
                        key,
                        $"{targetContract}|{targetInvoice}",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var parts = key.Split('|');
                foreach (var part in parts)
                {
                    if (string.Equals(part.Trim(), targetInvoice, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            var matched = tinLines
                .Where(MatchesInvoice)
                .OrderBy(x => x.ReceiptId)
                .ThenBy(x => x.LineNo)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.ReceiptId,
                    x.LineNo,
                    x.Item,
                    x.Account,
                    x.AccountCoaId,
                    x.PartyKey,
                    x.PartyType,
                    x.PartyId,
                    x.PartyCode,
                    x.PartyName,
                    x.PartyLabel,
                    x.ContractId,
                    x.InvoiceKey,
                    x.ContractNo,
                    x.InvoiceNo,
                    x.CollectionEntryId,
                    x.TinTrn,
                    x.TinFtn,
                    x.Amount,
                    x.UnitPrice,
                    x.Quantity,
                    x.ProductKey,
                    x.ProductType,
                    x.ProductId,
                    x.Discount,
                    x.Tax,
                    x.Total,
                })
                .ToList();

            return Ok(matched);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Receipt receipt)
        {
            if (receipt == null)
            {
                return BadRequest("Receipt is required.");
            }

            var incomingLines = ReceiptLineHelper.ResolveIncomingLines(receipt);
            var validation = ValidateReceipt(receipt, incomingLines);
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
            if (string.IsNullOrWhiteSpace(receipt.RecordType))
            {
                receipt.RecordType = "Receipt";
            }

            receipt.LinesJson = null;

            await _repository.AddAsync(receipt);

            await ReceiptLineHelper.ReplaceLinesAsync(
                _context,
                receipt,
                incomingLines,
                receipt.ActionBy);
            await _context.SaveChangesAsync();

            receipt.Lines = incomingLines;
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

            var incomingLines = ReceiptLineHelper.ResolveIncomingLines(receipt);
            var validation = ValidateReceipt(receipt, incomingLines);
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
            existing.PayeePartyId = receipt.PayeePartyId;
            existing.PayeePartyCode = receipt.PayeePartyCode;
            existing.PayeeName = receipt.PayeeName;
            existing.Description = receipt.Description;
            existing.GrandTotal = receipt.GrandTotal;
            existing.LinesJson = null;
            if (await CanFinalizeReceiptByAhqAsync())
            {
                existing.FinalizedByAhq = receipt.FinalizedByAhq == true;
            }
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, receipt.ActionBy);

            await ReceiptLineHelper.ReplaceLinesAsync(
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
            var existing = await _context.Receipts
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Receipt not found.");
            }

            if (existing.FinalizedByAhq == true && !await CanFinalizeReceiptByAhqAsync())
            {
                return Forbid();
            }

            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            await ClearLinkedCollectionEntriesAsync(existing);
            await ReceiptLineHelper.SoftDeleteLinesAsync(_context, id, existing.ActionBy);
            _context.Receipts.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// When a receipt is deleted, clear Vr No / Vr Date / ReceiptId on linked collections
        /// and set Status back to Pending.
        /// </summary>
        private async Task ClearLinkedCollectionEntriesAsync(Receipt receipt)
        {
            var receiptId = receipt.Id;
            var reference = (receipt.Reference ?? string.Empty).Trim();
            var vrNo = (receipt.VrNo ?? string.Empty).Trim();

            var linked = await _context.CollectionEntries
                .Where(x =>
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    (
                        x.ReceiptId == receiptId ||
                        (!string.IsNullOrWhiteSpace(reference) && x.VrNo == reference) ||
                        (!string.IsNullOrWhiteSpace(vrNo) && x.VrNo == vrNo)
                    ))
                .ToListAsync();

            if (linked.Count == 0)
            {
                return;
            }

            foreach (var entry in linked)
            {
                entry.VrNo = null;
                entry.VrDate = null;
                entry.ReceiptId = null;
                entry.Status = "Pending";
                entry.Action = "UPDATE";
                entry.ActionDate = DateTime.UtcNow;
                entry.ActionBy = receipt.ActionBy;
            }

            _context.CollectionEntries.UpdateRange(linked);
        }

        private static string? ValidateReceipt(Receipt receipt, IReadOnlyList<ReceiptLine> lines)
        {
            if (receipt.Date == null) return "Date is required.";
            if (string.IsNullOrWhiteSpace(receipt.Month) || !MonthYearPattern.IsMatch(receipt.Month.Trim()))
            {
                return "Month is required in MMM/YYYY format.";
            }

            if (string.IsNullOrWhiteSpace(receipt.PaidFrom)) return "Paid from is required.";
            if (string.IsNullOrWhiteSpace(receipt.PayeeName)) return "Payee is required.";
            if (receipt.GrandTotal == null || receipt.GrandTotal < 0) return "Grand total is required.";

            if (lines == null || lines.Count == 0)
            {
                return "At least one line item is required.";
            }

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

            return await DataAccessScopeHelper.IsAhqSupervisorAsync(User, _context);
        }
    }
}
