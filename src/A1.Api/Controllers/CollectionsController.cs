using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CollectionsController : ControllerBase
    {
        private readonly IGenericRepository<CollectionEntry> _repository;
        private readonly ApplicationDbContext _context;

        public CollectionsController(
            IGenericRepository<CollectionEntry> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.CollectionEntries
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.CollectionEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null)
            {
                return NotFound();
            }

            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CollectionEntry entry)
        {
            if (entry == null)
            {
                return BadRequest("Collection entry is required.");
            }

            var validation = ValidateEntry(entry);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            entry.IsDeleted = false;
            entry.ActionDate = DateTime.UtcNow;
            entry.Action = "CREATE";
            entry.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entry.ActionBy);
            ApplyComputedAmounts(entry);

            await _repository.AddAsync(entry);
            return CreatedAtAction(nameof(GetById), new { id = entry.Id }, entry);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CollectionEntry entry)
        {
            if (entry == null)
            {
                return BadRequest("Collection entry is required.");
            }

            if (entry.Id == 0)
            {
                entry.Id = id;
            }
            else if (entry.Id != id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.CollectionEntries
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Collection entry not found.");
            }

            var validation = ValidateEntry(entry);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            existing.ClassId = entry.ClassId;
            existing.ContractId = entry.ContractId;
            existing.ContractNo = entry.ContractNo;
            existing.TenantNo = entry.TenantNo;
            existing.TenantBusiness = entry.TenantBusiness;
            existing.CoaId = entry.CoaId;
            existing.InvoiceNo = entry.InvoiceNo;
            existing.ReceivableAmount = entry.ReceivableAmount;
            existing.DueAmount = entry.DueAmount;
            existing.BalanceAmount = entry.BalanceAmount;
            existing.CollectionDate = entry.CollectionDate;
            existing.Amount = entry.Amount;
            existing.TinTrn = entry.TinTrn;
            existing.Remarks = entry.Remarks;
            existing.Status = entry.Status;
            existing.VrNo = entry.VrNo;
            existing.VrDate = entry.VrDate;
            existing.ReceiptId = entry.ReceiptId;
            ApplyComputedAmounts(existing);
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entry.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.CollectionEntries
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Collection entry not found.");
            }

            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.CollectionEntries.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static void ApplyComputedAmounts(CollectionEntry entry)
        {
            var receivable = entry.ReceivableAmount ?? 0m;
            var amount = entry.Amount ?? 0m;
            entry.BalanceAmount = receivable - amount;
            entry.DueAmount = entry.BalanceAmount > 0 ? entry.BalanceAmount : 0m;
        }

        private static string? ValidateEntry(CollectionEntry entry)
        {
            var hasTenant =
                !string.IsNullOrWhiteSpace(entry.TenantNo) ||
                !string.IsNullOrWhiteSpace(entry.TenantBusiness);
            if (!hasTenant) return "Tenant and Business is required.";
            if (entry.CoaId == null) return "Account is required.";
            if (entry.Amount != null && entry.Amount < 0)
            {
                return "Amount must be greater than or equal to 0.";
            }

            return null;
        }
    }
}
