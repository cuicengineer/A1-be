using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchaseReturnsController : ControllerBase
    {
        private readonly IGenericRepository<PurchaseReturn> _repository;
        private readonly ApplicationDbContext _context;

        public PurchaseReturnsController(
            IGenericRepository<PurchaseReturn> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.PurchaseReturns
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
            var row = await _context.PurchaseReturns
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
        public async Task<IActionResult> Create([FromBody] PurchaseReturn purchaseReturn)
        {
            if (purchaseReturn == null)
            {
                return BadRequest("Purchase return is required.");
            }

            var validation = await ValidateAsync(purchaseReturn);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            NormalizePurchaseReturn(purchaseReturn);
            purchaseReturn.IsDeleted = false;
            purchaseReturn.ActionDate = DateTime.UtcNow;
            purchaseReturn.Action = "CREATE";
            purchaseReturn.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, purchaseReturn.ActionBy);

            await _repository.AddAsync(purchaseReturn);
            return CreatedAtAction(nameof(GetById), new { id = purchaseReturn.Id }, purchaseReturn);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] PurchaseReturn purchaseReturn)
        {
            if (purchaseReturn == null)
            {
                return BadRequest("Purchase return is required.");
            }

            if (purchaseReturn.Id == 0)
            {
                purchaseReturn.Id = id;
            }
            else if (purchaseReturn.Id != id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.PurchaseReturns
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Purchase return not found.");
            }

            var validation = await ValidateAsync(purchaseReturn, id);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            NormalizePurchaseReturn(purchaseReturn);
            existing.Date = purchaseReturn.Date;
            existing.VrNo = purchaseReturn.VrNo;
            existing.SupplierKey = purchaseReturn.SupplierKey;
            existing.SupplierLabel = purchaseReturn.SupplierLabel;
            existing.SupplierId = purchaseReturn.SupplierId;
            existing.SupplierCode = purchaseReturn.SupplierCode;
            existing.PurchaseInvoiceNo = purchaseReturn.PurchaseInvoiceNo;
            existing.PurchaseInvoiceLabel = purchaseReturn.PurchaseInvoiceLabel;
            existing.Description = purchaseReturn.Description;
            existing.GrandTotal = purchaseReturn.GrandTotal;
            existing.LinesJson = purchaseReturn.LinesJson;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, purchaseReturn.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.PurchaseReturns
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Purchase return not found.");
            }

            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.PurchaseReturns.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static void NormalizePurchaseReturn(PurchaseReturn purchaseReturn)
        {
            purchaseReturn.VrNo = (purchaseReturn.VrNo ?? string.Empty).Trim();
            purchaseReturn.SupplierKey = (purchaseReturn.SupplierKey ?? string.Empty).Trim();
            purchaseReturn.SupplierLabel = (purchaseReturn.SupplierLabel ?? string.Empty).Trim();
            purchaseReturn.SupplierCode = string.IsNullOrWhiteSpace(purchaseReturn.SupplierCode)
                ? null
                : purchaseReturn.SupplierCode.Trim();
            purchaseReturn.PurchaseInvoiceNo = (purchaseReturn.PurchaseInvoiceNo ?? string.Empty).Trim();
            purchaseReturn.PurchaseInvoiceLabel = string.IsNullOrWhiteSpace(purchaseReturn.PurchaseInvoiceLabel)
                ? purchaseReturn.PurchaseInvoiceNo
                : purchaseReturn.PurchaseInvoiceLabel.Trim();
            purchaseReturn.Description = string.IsNullOrWhiteSpace(purchaseReturn.Description)
                ? null
                : purchaseReturn.Description.Trim();
        }

        private static string? ValidatePurchaseReturn(PurchaseReturn purchaseReturn)
        {
            if (purchaseReturn.Date == null)
            {
                return "Date is required.";
            }

            if (string.IsNullOrWhiteSpace(purchaseReturn.VrNo))
            {
                return "Vr No is required.";
            }

            if (string.IsNullOrWhiteSpace(purchaseReturn.SupplierKey))
            {
                return "Supplier is required.";
            }

            if (string.IsNullOrWhiteSpace(purchaseReturn.PurchaseInvoiceNo))
            {
                return "Purchase Invoice is required.";
            }

            if (purchaseReturn.GrandTotal == null || purchaseReturn.GrandTotal < 0)
            {
                return "Grand total is required.";
            }

            if (string.IsNullOrWhiteSpace(purchaseReturn.LinesJson) || purchaseReturn.LinesJson.Trim() == "[]")
            {
                return "At least one line item is required.";
            }

            return null;
        }

        private async Task<string?> ValidateAsync(PurchaseReturn purchaseReturn, int? excludeId = null)
        {
            var validation = ValidatePurchaseReturn(purchaseReturn);
            if (validation != null)
            {
                return validation;
            }

            var vrNo = (purchaseReturn.VrNo ?? string.Empty).Trim();
            var duplicate = await _context.PurchaseReturns.AsNoTracking().AnyAsync(x =>
                x.VrNo != null &&
                x.VrNo.ToUpper() == vrNo.ToUpper() &&
                (x.IsDeleted == null || x.IsDeleted == false) &&
                (!excludeId.HasValue || x.Id != excludeId.Value));
            if (duplicate)
            {
                return "Vr No must be unique.";
            }

            return null;
        }
    }
}
