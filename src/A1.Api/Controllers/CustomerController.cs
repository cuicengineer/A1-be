using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

            return Ok(customers);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));

            if (customer == null)
            {
                return NotFound();
            }

            return Ok(customer);
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

            var activeWithSameCode = await _context.Customers.IgnoreQueryFilters()
                .AnyAsync(c => c.Code == code && (c.IsDeleted == null || c.IsDeleted == false));
            if (activeWithSameCode)
            {
                return Conflict("A customer with this Code already exists.");
            }

            var deletedWithSameCode = await _context.Customers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Code == code && c.IsDeleted == true);
            if (deletedWithSameCode != null)
            {
                deletedWithSameCode.Code = $"{code}~{deletedWithSameCode.Id}";
                await _context.SaveChangesAsync();
            }

            customer.Code = code;
            customer.IsDeleted = false;
            customer.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, customer.ActionBy);

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

            var duplicateCode = await _context.Customers
                .AnyAsync(c =>
                    c.Id != id &&
                    c.Code == code &&
                    (c.IsDeleted == null || c.IsDeleted == false));
            if (duplicateCode)
            {
                return Conflict("A customer with this Code already exists.");
            }

            existing.Code = code;
            existing.Prefix = customer.Prefix;
            existing.Rank = customer.Rank;
            existing.Name = customer.Name;
            existing.Address = customer.Address;
            existing.Province = customer.Province;
            existing.City = customer.City;
            existing.NtnCnic = customer.NtnCnic;
            existing.GSTNo = customer.GSTNo;
            existing.TelNo = customer.TelNo;
            existing.MobileNo = customer.MobileNo;
            existing.CoaId = customer.CoaId;
            existing.Representative = customer.Representative;
            existing.BankListsId = customer.BankListsId;
            existing.IBAN = string.IsNullOrWhiteSpace(customer.IBAN) ? null : customer.IBAN.Trim();
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, customer.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
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
    }
}
