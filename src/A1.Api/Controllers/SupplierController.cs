using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

            return Ok(suppliers);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && (s.IsDeleted == null || s.IsDeleted == false));

            if (supplier == null)
            {
                return NotFound();
            }

            return Ok(supplier);
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

            var activeWithSameCode = await _context.Suppliers.IgnoreQueryFilters()
                .AnyAsync(s => s.Code == code && (s.IsDeleted == null || s.IsDeleted == false));
            if (activeWithSameCode)
            {
                return Conflict("A supplier with this Code already exists.");
            }

            var deletedWithSameCode = await _context.Suppliers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Code == code && s.IsDeleted == true);
            if (deletedWithSameCode != null)
            {
                deletedWithSameCode.Code = $"{code}~{deletedWithSameCode.Id}";
                await _context.SaveChangesAsync();
            }

            supplier.Code = code;
            supplier.IsDeleted = false;
            supplier.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, supplier.ActionBy);

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

            var duplicateCode = await _context.Suppliers
                .AnyAsync(s =>
                    s.Id != id &&
                    s.Code == code &&
                    (s.IsDeleted == null || s.IsDeleted == false));
            if (duplicateCode)
            {
                return Conflict("A supplier with this Code already exists.");
            }

            existing.Code = code;
            existing.Prefix = supplier.Prefix;
            existing.Rank = supplier.Rank;
            existing.Name = supplier.Name;
            existing.Address = supplier.Address;
            existing.Province = supplier.Province;
            existing.City = supplier.City;
            existing.NtnCnic = supplier.NtnCnic;
            existing.GSTNo = supplier.GSTNo;
            existing.TelNo = supplier.TelNo;
            existing.MobileNo = supplier.MobileNo;
            existing.CoaId = supplier.CoaId;
            existing.Representative = supplier.Representative;
            existing.BankListsId = supplier.BankListsId;
            existing.IBAN = string.IsNullOrWhiteSpace(supplier.IBAN) ? null : supplier.IBAN.Trim();
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, supplier.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
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
    }
}
