using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SupplierController : ControllerBase
    {
        private readonly IGenericRepository<Supplier> _repository;
        private readonly ApplicationDbContext _context;

        public SupplierController(IGenericRepository<Supplier> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.IsDeleted == null || s.IsDeleted == false)
                .OrderByDescending(s => s.Id)
                .ToListAsync();

            var dealerLookup = await GetDealerLookupAsync(suppliers.Select(s => s.DealerId));
            return Ok(suppliers.Select(supplier => BuildSupplierResponse(supplier, dealerLookup)));
        }

        [HttpGet("byCode")]
        public async Task<IActionResult> GetByCode([FromQuery] string code, [FromQuery] int? excludeId = null)
        {
            var normalizedCode = (code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                return BadRequest("Code is required.");
            }

            var query = _context.Suppliers
                .AsNoTracking()
                .Where(s =>
                    s.Code == normalizedCode &&
                    (s.IsDeleted == null || s.IsDeleted == false) &&
                    s.Status);

            if (excludeId.HasValue)
            {
                query = query.Where(s => s.Id != excludeId.Value);
            }

            var supplier = await query.FirstOrDefaultAsync();
            if (supplier == null)
            {
                return NotFound();
            }

            var dealerLookup = await GetDealerLookupAsync(new[] { supplier.DealerId });
            return Ok(BuildSupplierResponse(supplier, dealerLookup));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && (s.IsDeleted == null || s.IsDeleted == false));

            if (supplier == null)
            {
                return NotFound();
            }

            var dealerLookup = await GetDealerLookupAsync(new[] { supplier.DealerId });
            return Ok(BuildSupplierResponse(supplier, dealerLookup));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Supplier supplier)
        {
            if (supplier == null)
            {
                return BadRequest("Supplier data is required.");
            }

            var code = (supplier.Code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest("Code is required.");
            }

            var dealer = await _context.Dealers
                .FirstOrDefaultAsync(d =>
                    d.Id == supplier.DealerId &&
                    (d.IsDeleted == null || d.IsDeleted == false) &&
                    d.Status);
            if (dealer == null)
            {
                return BadRequest("A valid active dealer is required.");
            }

            var activeWithSameCode = await _context.Suppliers.IgnoreQueryFilters()
                .AnyAsync(s =>
                    s.Code == code &&
                    (s.IsDeleted == null || s.IsDeleted == false) &&
                    s.Status);
            if (activeWithSameCode)
            {
                return Conflict("A supplier with this Code already exists.");
            }

            var activeWithSameDealer = await _context.Suppliers.IgnoreQueryFilters()
                .AnyAsync(s =>
                    s.DealerId == dealer.Id &&
                    (s.IsDeleted == null || s.IsDeleted == false) &&
                    s.Status);
            if (activeWithSameDealer)
            {
                return Conflict("A supplier for this dealer already exists.");
            }

            var deletedWithSameCode = await _context.Suppliers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Code == code && s.IsDeleted == true);
            if (deletedWithSameCode != null)
            {
                deletedWithSameCode.Code = $"{code}~{deletedWithSameCode.Id}";
                await _context.SaveChangesAsync();
            }

            supplier.Code = code;
            ApplyDealerToSupplier(supplier, dealer);
            supplier.IsDeleted = false;
            supplier.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, supplier.ActionBy);

            var coaError = PartyControlAccountValidation.ValidateDualCoaIds(supplier.CoaId, supplier.CoaId2);
            if (coaError != null)
            {
                return BadRequest(coaError);
            }

            await _repository.AddAsync(supplier);
            return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, supplier);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Supplier supplier)
        {
            if (supplier == null)
            {
                return BadRequest("Supplier data is required.");
            }

            if (supplier.Id == 0)
            {
                supplier.Id = id;
            }
            else if (id != supplier.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id && (s.IsDeleted == null || s.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Supplier not found.");
            }

            var code = (supplier.Code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest("Code is required.");
            }

            if (!string.Equals(existing.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Code cannot be changed.");
            }

            var duplicateCode = await _context.Suppliers
                .AnyAsync(s =>
                    s.Id != id &&
                    s.Code == code &&
                    (s.IsDeleted == null || s.IsDeleted == false) &&
                    s.Status);
            if (duplicateCode)
            {
                return Conflict("A supplier with this Code already exists.");
            }

            var dealerId = existing.DealerId ?? supplier.DealerId;
            if (!dealerId.HasValue)
            {
                return BadRequest("Dealer link is required.");
            }

            if (supplier.DealerId.HasValue && supplier.DealerId.Value != dealerId.Value)
            {
                return BadRequest("Dealer cannot be changed.");
            }

            var dealer = await _context.Dealers
                .FirstOrDefaultAsync(d =>
                    d.Id == dealerId.Value &&
                    (d.IsDeleted == null || d.IsDeleted == false));
            if (dealer == null)
            {
                return BadRequest("Dealer not found.");
            }

            var duplicateDealer = await _context.Suppliers
                .AnyAsync(s =>
                    s.Id != id &&
                    s.DealerId == dealer.Id &&
                    (s.IsDeleted == null || s.IsDeleted == false) &&
                    s.Status);
            if (duplicateDealer)
            {
                return Conflict("A supplier for this dealer already exists.");
            }

            existing.Code = code;
            existing.DealerId = dealer.Id;
            ApplyDealerToSupplier(existing, dealer);
            existing.CoaId = supplier.CoaId;
            existing.CoaId2 = supplier.CoaId2;
            existing.Status = supplier.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, supplier.ActionBy);

            var coaError = PartyControlAccountValidation.ValidateDualCoaIds(existing.CoaId, existing.CoaId2);
            if (coaError != null)
            {
                return BadRequest(coaError);
            }

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpPatch("bulkStatus")]
        public async Task<IActionResult> BulkUpdateStatus([FromBody] BulkPartyStatusRequest request)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
            {
                return BadRequest("At least one supplier ID is required.");
            }

            var suppliers = await _context.Suppliers
                .Where(s =>
                    request.Ids.Contains(s.Id) &&
                    (s.IsDeleted == null || s.IsDeleted == false))
                .ToListAsync();

            if (suppliers.Count == 0)
            {
                return NotFound("No matching suppliers found.");
            }

            foreach (var supplier in suppliers)
            {
                supplier.Status = request.Status;
                supplier.ActionDate = DateTime.UtcNow;
                supplier.Action = "UPDATE";
                supplier.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, supplier.ActionBy);
            }

            await _context.SaveChangesAsync();
            return Ok(new { updated = suppliers.Count });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id && (s.IsDeleted == null || s.IsDeleted == false));

            if (supplier == null)
            {
                return NotFound("Supplier not found.");
            }

            supplier.IsDeleted = true;
            supplier.Action = "DELETE";
            supplier.ActionDate = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(supplier.ActionBy))
            {
                var existingActionBy = await _context.Suppliers
                    .AsNoTracking()
                    .Where(s => s.Id == id)
                    .Select(s => s.ActionBy)
                    .FirstOrDefaultAsync();
                supplier.ActionBy = existingActionBy;
            }
            supplier.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, supplier.ActionBy);

            _context.Suppliers.Update(supplier);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<Dictionary<int, Dealer>> GetDealerLookupAsync(IEnumerable<int?> dealerIds)
        {
            var ids = dealerIds
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<int, Dealer>();
            }

            return await _context.Dealers
                .AsNoTracking()
                .Where(d =>
                    ids.Contains(d.Id) &&
                    (d.IsDeleted == null || d.IsDeleted == false))
                .ToDictionaryAsync(d => d.Id);
        }

        private static object BuildSupplierResponse(Supplier supplier, IReadOnlyDictionary<int, Dealer> dealerLookup)
        {
            Dealer? dealer = null;
            if (supplier.DealerId.HasValue)
            {
                dealerLookup.TryGetValue(supplier.DealerId.Value, out dealer);
            }

            return new
            {
                supplier.Id,
                supplier.ActionDate,
                supplier.ActionBy,
                supplier.Action,
                supplier.IsDeleted,
                supplier.Code,
                supplier.DealerId,
                DealerName = dealer?.Name,
                Prefix = dealer?.Prefix ?? supplier.Prefix,
                Rank = dealer?.Rank ?? supplier.Rank,
                Name = dealer?.Name ?? supplier.Name,
                Address = dealer?.Address ?? supplier.Address,
                Province = dealer?.Province ?? supplier.Province,
                City = dealer?.City ?? supplier.City,
                NtnCnic = dealer?.NtnCnic ?? supplier.NtnCnic,
                GSTNo = dealer?.GSTNo ?? supplier.GSTNo,
                TelNo = dealer?.TelNo ?? supplier.TelNo,
                MobileNo = dealer?.MobileNo ?? supplier.MobileNo,
                supplier.CoaId,
                supplier.CoaId2,
                Representative = dealer?.Representative ?? supplier.Representative,
                TitleAccount = dealer?.TitleAccount ?? supplier.TitleAccount,
                BankListsId = dealer?.BankListsId ?? supplier.BankListsId,
                IBAN = dealer?.IBAN ?? supplier.IBAN,
                Status = supplier.Status
            };
        }

        private static void ApplyDealerToSupplier(Supplier supplier, Dealer dealer)
        {
            supplier.DealerId = dealer.Id;
            supplier.Prefix = dealer.Prefix;
            supplier.Rank = dealer.Rank;
            supplier.Name = dealer.Name;
            supplier.Address = dealer.Address;
            supplier.Province = dealer.Province;
            supplier.City = dealer.City;
            supplier.NtnCnic = dealer.NtnCnic;
            supplier.GSTNo = dealer.GSTNo;
            supplier.TelNo = dealer.TelNo;
            supplier.MobileNo = dealer.MobileNo;
            supplier.Representative = dealer.Representative;
            supplier.TitleAccount = dealer.TitleAccount;
            supplier.BankListsId = dealer.BankListsId;
            supplier.IBAN = dealer.IBAN;
        }
    }
}
