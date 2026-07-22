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
    public class CustomerController : ControllerBase
    {
        private readonly IGenericRepository<Customer> _repository;
        private readonly ApplicationDbContext _context;

        public CustomerController(IGenericRepository<Customer> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var customers = await _context.Customers
                .AsNoTracking()
                .Where(c => c.IsDeleted == null || c.IsDeleted == false)
                .OrderByDescending(c => c.Id)
                .ToListAsync();

            var dealerLookup = await GetDealerLookupAsync(customers.Select(c => c.DealerId));
            return Ok(customers.Select(customer => BuildCustomerResponse(customer, dealerLookup)));
        }

        [HttpGet("byCode")]
        public async Task<IActionResult> GetByCode([FromQuery] string code, [FromQuery] int? excludeId = null)
        {
            var normalizedCode = (code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                return BadRequest("Code is required.");
            }

            var query = _context.Customers
                .AsNoTracking()
                .Where(c =>
                    c.Code == normalizedCode &&
                    (c.IsDeleted == null || c.IsDeleted == false) &&
                    c.Status);

            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }

            var customer = await query.FirstOrDefaultAsync();
            if (customer == null)
            {
                return NotFound();
            }

            var dealerLookup = await GetDealerLookupAsync(new[] { customer.DealerId });
            return Ok(BuildCustomerResponse(customer, dealerLookup));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));

            if (customer == null)
            {
                return NotFound();
            }

            var dealerLookup = await GetDealerLookupAsync(new[] { customer.DealerId });
            return Ok(BuildCustomerResponse(customer, dealerLookup));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Customer customer)
        {
            if (customer == null)
            {
                return BadRequest("Customer data is required.");
            }

            var code = (customer.Code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest("Code is required.");
            }

            var dealer = await _context.Dealers
                .FirstOrDefaultAsync(d =>
                    d.Id == customer.DealerId &&
                    (d.IsDeleted == null || d.IsDeleted == false) &&
                    d.Status);
            if (dealer == null)
            {
                return BadRequest("A valid active dealer is required.");
            }

            var activeWithSameCode = await _context.Customers.IgnoreQueryFilters()
                .AnyAsync(c =>
                    c.Code == code &&
                    (c.IsDeleted == null || c.IsDeleted == false) &&
                    c.Status);
            if (activeWithSameCode)
            {
                return Conflict("A customer with this Code already exists.");
            }

            var activeWithSameDealer = await _context.Customers.IgnoreQueryFilters()
                .AnyAsync(c =>
                    c.DealerId == dealer.Id &&
                    (c.IsDeleted == null || c.IsDeleted == false) &&
                    c.Status);
            if (activeWithSameDealer)
            {
                return Conflict("A customer for this dealer already exists.");
            }

            var deletedWithSameCode = await _context.Customers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Code == code && c.IsDeleted == true);
            if (deletedWithSameCode != null)
            {
                deletedWithSameCode.Code = $"{code}~{deletedWithSameCode.Id}";
                await _context.SaveChangesAsync();
            }

            customer.Code = code;
            ApplyDealerToCustomer(customer, dealer);
            customer.IsDeleted = false;
            customer.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, customer.ActionBy);

            var coaError = PartyControlAccountValidation.ValidateDualCoaIds(customer.CoaId, customer.CoaId2);
            if (coaError != null)
            {
                return BadRequest(coaError);
            }

            await _repository.AddAsync(customer);
            return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Customer customer)
        {
            if (customer == null)
            {
                return BadRequest("Customer data is required.");
            }

            if (customer.Id == 0)
            {
                customer.Id = id;
            }
            else if (id != customer.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Customer not found.");
            }

            var code = (customer.Code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest("Code is required.");
            }

            if (!string.Equals(existing.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Code cannot be changed.");
            }

            var duplicateCode = await _context.Customers
                .AnyAsync(c =>
                    c.Id != id &&
                    c.Code == code &&
                    (c.IsDeleted == null || c.IsDeleted == false) &&
                    c.Status);
            if (duplicateCode)
            {
                return Conflict("A customer with this Code already exists.");
            }

            var dealerId = existing.DealerId ?? customer.DealerId;
            if (!dealerId.HasValue)
            {
                return BadRequest("Dealer link is required.");
            }

            if (customer.DealerId.HasValue && customer.DealerId.Value != dealerId.Value)
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

            var duplicateDealer = await _context.Customers
                .AnyAsync(c =>
                    c.Id != id &&
                    c.DealerId == dealer.Id &&
                    (c.IsDeleted == null || c.IsDeleted == false) &&
                    c.Status);
            if (duplicateDealer)
            {
                return Conflict("A customer for this dealer already exists.");
            }

            existing.Code = code;
            existing.DealerId = dealer.Id;
            ApplyDealerToCustomer(existing, dealer);
            existing.CoaId = customer.CoaId;
            existing.CoaId2 = customer.CoaId2;
            existing.Status = customer.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, customer.ActionBy);

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
                return BadRequest("At least one customer ID is required.");
            }

            var customers = await _context.Customers
                .Where(c =>
                    request.Ids.Contains(c.Id) &&
                    (c.IsDeleted == null || c.IsDeleted == false))
                .ToListAsync();

            if (customers.Count == 0)
            {
                return NotFound("No matching customers found.");
            }

            foreach (var customer in customers)
            {
                customer.Status = request.Status;
                customer.ActionDate = DateTime.UtcNow;
                customer.Action = "UPDATE";
                customer.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, customer.ActionBy);
            }

            await _context.SaveChangesAsync();
            return Ok(new { updated = customers.Count });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));

            if (customer == null)
            {
                return NotFound("Customer not found.");
            }

            customer.IsDeleted = true;
            customer.Action = "DELETE";
            customer.ActionDate = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(customer.ActionBy))
            {
                var existingActionBy = await _context.Customers
                    .AsNoTracking()
                    .Where(c => c.Id == id)
                    .Select(c => c.ActionBy)
                    .FirstOrDefaultAsync();
                customer.ActionBy = existingActionBy;
            }
            customer.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, customer.ActionBy);

            _context.Customers.Update(customer);
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

        private static object BuildCustomerResponse(Customer customer, IReadOnlyDictionary<int, Dealer> dealerLookup)
        {
            Dealer? dealer = null;
            if (customer.DealerId.HasValue)
            {
                dealerLookup.TryGetValue(customer.DealerId.Value, out dealer);
            }

            return new
            {
                customer.Id,
                customer.ActionDate,
                customer.ActionBy,
                customer.Action,
                customer.IsDeleted,
                customer.Code,
                customer.DealerId,
                DealerName = dealer?.Name,
                Prefix = dealer?.Prefix ?? customer.Prefix,
                Rank = dealer?.Rank ?? customer.Rank,
                Name = dealer?.Name ?? customer.Name,
                Address = dealer?.Address ?? customer.Address,
                Province = dealer?.Province ?? customer.Province,
                City = dealer?.City ?? customer.City,
                NtnCnic = dealer?.NtnCnic ?? customer.NtnCnic,
                GSTNo = dealer?.GSTNo ?? customer.GSTNo,
                TelNo = dealer?.TelNo ?? customer.TelNo,
                MobileNo = dealer?.MobileNo ?? customer.MobileNo,
                customer.CoaId,
                customer.CoaId2,
                Representative = dealer?.Representative ?? customer.Representative,
                TitleAccount = dealer?.TitleAccount ?? customer.TitleAccount,
                BankListsId = dealer?.BankListsId ?? customer.BankListsId,
                IBAN = dealer?.IBAN ?? customer.IBAN,
                Status = customer.Status
            };
        }

        private static void ApplyDealerToCustomer(Customer customer, Dealer dealer)
        {
            customer.DealerId = dealer.Id;
            customer.Prefix = dealer.Prefix;
            customer.Rank = dealer.Rank;
            customer.Name = dealer.Name;
            customer.Address = dealer.Address;
            customer.Province = dealer.Province;
            customer.City = dealer.City;
            customer.NtnCnic = dealer.NtnCnic;
            customer.GSTNo = dealer.GSTNo;
            customer.TelNo = dealer.TelNo;
            customer.MobileNo = dealer.MobileNo;
            customer.Representative = dealer.Representative;
            customer.TitleAccount = dealer.TitleAccount;
            customer.BankListsId = dealer.BankListsId;
            customer.IBAN = dealer.IBAN;
        }
    }
}
