using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesReturnsController : ControllerBase
    {
        private readonly IGenericRepository<SalesReturn> _repository;
        private readonly ApplicationDbContext _context;

        public SalesReturnsController(
            IGenericRepository<SalesReturn> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.SalesReturns
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
            var row = await _context.SalesReturns
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
        public async Task<IActionResult> Create([FromBody] SalesReturn salesReturn)
        {
            if (salesReturn == null)
            {
                return BadRequest("Sales return is required.");
            }

            var validation = ValidateSalesReturn(salesReturn);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            NormalizeSalesReturn(salesReturn);
            salesReturn.IsDeleted = false;
            salesReturn.ActionDate = DateTime.UtcNow;
            salesReturn.Action = "CREATE";
            salesReturn.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, salesReturn.ActionBy);

            await _repository.AddAsync(salesReturn);
            return CreatedAtAction(nameof(GetById), new { id = salesReturn.Id }, salesReturn);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SalesReturn salesReturn)
        {
            if (salesReturn == null)
            {
                return BadRequest("Sales return is required.");
            }

            if (salesReturn.Id == 0)
            {
                salesReturn.Id = id;
            }
            else if (salesReturn.Id != id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.SalesReturns
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Sales return not found.");
            }

            var validation = ValidateSalesReturn(salesReturn);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            NormalizeSalesReturn(salesReturn);
            existing.Date = salesReturn.Date;
            existing.VrNo = salesReturn.VrNo;
            existing.ContractCustomerKey = salesReturn.ContractCustomerKey;
            existing.ContractCustomerLabel = salesReturn.ContractCustomerLabel;
            existing.ContractId = salesReturn.ContractId;
            existing.ContractNo = salesReturn.ContractNo;
            existing.CustomerId = salesReturn.CustomerId;
            existing.InvoiceKey = salesReturn.InvoiceKey;
            existing.InvoiceNo = salesReturn.InvoiceNo;
            existing.InvoiceLabel = salesReturn.InvoiceLabel;
            existing.Description = salesReturn.Description;
            existing.GrandTotal = salesReturn.GrandTotal;
            existing.LinesJson = salesReturn.LinesJson;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, salesReturn.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.SalesReturns
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null)
            {
                return NotFound("Sales return not found.");
            }

            existing.IsDeleted = true;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "DELETE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.SalesReturns.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static void NormalizeSalesReturn(SalesReturn salesReturn)
        {
            salesReturn.VrNo = (salesReturn.VrNo ?? string.Empty).Trim();
            salesReturn.ContractCustomerKey = (salesReturn.ContractCustomerKey ?? string.Empty).Trim();
            salesReturn.ContractCustomerLabel = (salesReturn.ContractCustomerLabel ?? string.Empty).Trim();
            salesReturn.ContractNo = string.IsNullOrWhiteSpace(salesReturn.ContractNo)
                ? null
                : salesReturn.ContractNo.Trim();
            salesReturn.InvoiceKey = (salesReturn.InvoiceKey ?? string.Empty).Trim();
            salesReturn.InvoiceNo = (salesReturn.InvoiceNo ?? string.Empty).Trim();
            salesReturn.InvoiceLabel = (salesReturn.InvoiceLabel ?? string.Empty).Trim();
            salesReturn.Description = string.IsNullOrWhiteSpace(salesReturn.Description)
                ? null
                : salesReturn.Description.Trim();
        }

        private static string? ValidateSalesReturn(SalesReturn salesReturn)
        {
            if (salesReturn.Date == null)
            {
                return "Date is required.";
            }

            if (string.IsNullOrWhiteSpace(salesReturn.VrNo))
            {
                return "Vr No is required.";
            }

            if (string.IsNullOrWhiteSpace(salesReturn.ContractCustomerKey))
            {
                return "Contract / Customer is required.";
            }

            if (string.IsNullOrWhiteSpace(salesReturn.InvoiceKey))
            {
                return "Sales Invoice is required.";
            }

            if (salesReturn.GrandTotal == null || salesReturn.GrandTotal < 0)
            {
                return "Grand total is required.";
            }

            if (string.IsNullOrWhiteSpace(salesReturn.LinesJson) || salesReturn.LinesJson.Trim() == "[]")
            {
                return "At least one line item is required.";
            }

            return null;
        }
    }
}
