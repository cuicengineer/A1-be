using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchaseInvoicesController : ControllerBase
    {
        private readonly IGenericRepository<PurchaseInvoice> _repository;
        private readonly ApplicationDbContext _context;

        public PurchaseInvoicesController(
            IGenericRepository<PurchaseInvoice> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.PurchaseInvoices
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderByDescending(x => x.Date)
                .ThenByDescending(x => x.Id)
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.PurchaseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null)
            {
                return NotFound();
            }

            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PurchaseInvoice purchaseInvoice)
        {
            if (purchaseInvoice == null)
            {
                return BadRequest("Purchase invoice is required.");
            }

            var validation = await ValidateAsync(purchaseInvoice);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            NormalizePurchaseInvoice(purchaseInvoice);
            purchaseInvoice.IsDeleted = false;
            purchaseInvoice.ActionDate = DateTime.UtcNow;
            purchaseInvoice.Action = "CREATE";
            purchaseInvoice.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, purchaseInvoice.ActionBy);

            await _repository.AddAsync(purchaseInvoice);
            return CreatedAtAction(nameof(GetById), new { id = purchaseInvoice.Id }, purchaseInvoice);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] PurchaseInvoice purchaseInvoice)
        {
            if (purchaseInvoice == null)
            {
                return BadRequest("Purchase invoice is required.");
            }

            if (purchaseInvoice.Id == 0)
            {
                purchaseInvoice.Id = id;
            }
            else if (purchaseInvoice.Id != id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.PurchaseInvoices
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Purchase invoice not found.");
            }

            var validation = await ValidateAsync(purchaseInvoice, id);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            NormalizePurchaseInvoice(purchaseInvoice);
            existing.Date = purchaseInvoice.Date;
            existing.PiNo = purchaseInvoice.PiNo;
            existing.Description = purchaseInvoice.Description;
            existing.GrandTotal = purchaseInvoice.GrandTotal;
            existing.LinesJson = purchaseInvoice.LinesJson;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, purchaseInvoice.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.PurchaseInvoices
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Purchase invoice not found.");
            }

            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.PurchaseInvoices.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static void NormalizePurchaseInvoice(PurchaseInvoice purchaseInvoice)
        {
            purchaseInvoice.PiNo = (purchaseInvoice.PiNo ?? string.Empty).Trim();
            purchaseInvoice.Description = string.IsNullOrWhiteSpace(purchaseInvoice.Description)
                ? null
                : purchaseInvoice.Description.Trim();
        }

        private static string? ValidatePurchaseInvoice(PurchaseInvoice purchaseInvoice)
        {
            if (purchaseInvoice.Date == null)
            {
                return "Date is required.";
            }

            if (string.IsNullOrWhiteSpace(purchaseInvoice.PiNo))
            {
                return "PI No is required.";
            }

            if (purchaseInvoice.GrandTotal == null || purchaseInvoice.GrandTotal < 0)
            {
                return "Grand total is required.";
            }

            if (string.IsNullOrWhiteSpace(purchaseInvoice.LinesJson) || purchaseInvoice.LinesJson.Trim() == "[]")
            {
                return "At least one line item is required.";
            }

            return null;
        }

        private async Task<string?> ValidateAsync(PurchaseInvoice purchaseInvoice, int? excludeId = null)
        {
            var validation = ValidatePurchaseInvoice(purchaseInvoice);
            if (validation != null)
            {
                return validation;
            }

            var piNo = (purchaseInvoice.PiNo ?? string.Empty).Trim();
            var duplicate = await _context.PurchaseInvoices.AsNoTracking().AnyAsync(x =>
                x.PiNo != null &&
                x.PiNo.ToUpper() == piNo.ToUpper() &&
                (x.IsDeleted == null || x.IsDeleted == false) &&
                (!excludeId.HasValue || x.Id != excludeId.Value));
            if (duplicate)
            {
                return "PI No must be unique.";
            }

            return null;
        }
    }
}
