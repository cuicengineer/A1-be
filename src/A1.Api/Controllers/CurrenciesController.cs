using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CurrenciesController : ControllerBase
    {
        private static readonly string[] AllowedTypes = { "Base", "Foreign" };

        private readonly IGenericRepository<Currency> _repository;
        private readonly ApplicationDbContext _context;

        public CurrenciesController(IGenericRepository<Currency> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 0)
        {
            pageNumber = PaginationHelper.NormalizePageNumber(pageNumber);

            var baseQuery = _context.Currencies
                .AsNoTracking()
                .Where(c => c.IsDeleted == null || c.IsDeleted == false);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = PaginationHelper.FormatPageSizeHeader(pageSize, totalCount);

            var rows = await PaginationHelper.ApplyPaging(
                    baseQuery.OrderByDescending(c => c.Id),
                    pageNumber,
                    pageSize)
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.Currencies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));

            if (row == null)
            {
                return NotFound();
            }

            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Currency item)
        {
            if (item == null)
            {
                return BadRequest("Currency data is required.");
            }

            var validationError = await ValidateCurrencyAsync(item);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            NormalizeCurrency(item);
            item.IsDeleted = false;
            item.ActionDate = DateTime.UtcNow;
            item.Action = "CREATE";
            item.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.AddAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Currency item)
        {
            if (item == null)
            {
                return BadRequest("Currency data is required.");
            }

            if (item.Id == 0)
            {
                item.Id = id;
            }
            else if (id != item.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var validationError = await ValidateCurrencyAsync(item, id);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var existing = await _context.Currencies
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Currency not found.");
            }

            NormalizeCurrency(item);
            existing.Code = item.Code;
            existing.Name = item.Name;
            existing.CurrencyType = item.CurrencyType;
            existing.DecimalPlaces = item.DecimalPlaces;
            existing.Status = item.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.Currencies
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Currency not found.");
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(existing.ActionBy))
            {
                var existingActionBy = await _context.Currencies
                    .AsNoTracking()
                    .Where(c => c.Id == id)
                    .Select(c => c.ActionBy)
                    .FirstOrDefaultAsync();
                existing.ActionBy = existingActionBy;
            }
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.Currencies.Update(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static void NormalizeCurrency(Currency item)
        {
            item.Code = (item.Code ?? string.Empty).Trim().ToUpperInvariant();
            item.Name = (item.Name ?? string.Empty).Trim();
            item.CurrencyType = string.IsNullOrWhiteSpace(item.CurrencyType) ? "Base" : item.CurrencyType.Trim();
            if (!item.Status.HasValue)
            {
                item.Status = 1;
            }
        }

        private async Task<string?> ValidateCurrencyAsync(Currency item, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(item.Code))
            {
                return "Code is required.";
            }

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                return "Name is required.";
            }

            var type = string.IsNullOrWhiteSpace(item.CurrencyType) ? "Base" : item.CurrencyType.Trim();
            if (!AllowedTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            {
                return "Type must be Base or Foreign.";
            }

            if (item.DecimalPlaces < 0)
            {
                return "Decimal must be zero or greater.";
            }

            if (!item.Status.HasValue)
            {
                return "Status is required.";
            }

            if (item.Status != 0 && item.Status != 1)
            {
                return "Status must be 0 (Inactive) or 1 (Active).";
            }

            var code = item.Code.Trim().ToUpperInvariant();
            var duplicate = await _context.Currencies
                .AsNoTracking()
                .AnyAsync(c =>
                    c.Code == code &&
                    (c.IsDeleted == null || c.IsDeleted == false) &&
                    (!excludeId.HasValue || c.Id != excludeId.Value));

            if (duplicate)
            {
                return "A currency with this code already exists.";
            }

            return null;
        }
    }
}
