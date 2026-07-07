using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExchangeRatesController : ControllerBase
    {
        private readonly IGenericRepository<ExchangeRate> _repository;
        private readonly ApplicationDbContext _context;

        public ExchangeRatesController(IGenericRepository<ExchangeRate> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 0)
        {
            pageNumber = PaginationHelper.NormalizePageNumber(pageNumber);

            var baseQuery = _context.ExchangeRates
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = PaginationHelper.FormatPageSizeHeader(pageSize, totalCount);

            var pageRowsQuery =
                from row in baseQuery
                join baseCur in _context.Currencies.Where(c => c.IsDeleted == null || c.IsDeleted == false)
                    on row.BaseCurrencyId equals baseCur.Id into baseGroup
                from baseCur in baseGroup.DefaultIfEmpty()
                join foreignCur in _context.Currencies.Where(c => c.IsDeleted == null || c.IsDeleted == false)
                    on row.ForeignCurrencyId equals foreignCur.Id into foreignGroup
                from foreignCur in foreignGroup.DefaultIfEmpty()
                orderby row.RateDate descending, row.Id descending
                select new
                {
                    row,
                    BaseCode = baseCur != null ? baseCur.Code : string.Empty,
                    BaseName = baseCur != null ? baseCur.Name : string.Empty,
                    ForeignCode = foreignCur != null ? foreignCur.Code : string.Empty,
                    ForeignName = foreignCur != null ? foreignCur.Name : string.Empty
                };

            var pageRows = await PaginationHelper.ApplyPaging(pageRowsQuery, pageNumber, pageSize)
                .ToListAsync();

            var payload = new List<ExchangeRate>(pageRows.Count);
            foreach (var item in pageRows)
            {
                var baseCode = (item.BaseCode ?? string.Empty).Trim();
                var baseName = (item.BaseName ?? string.Empty).Trim();
                var foreignCode = (item.ForeignCode ?? string.Empty).Trim();
                var foreignName = (item.ForeignName ?? string.Empty).Trim();

                item.row.BaseCurrencyCode = baseCode;
                item.row.BaseCurrencyDisplay = FormatCurrencyLabel(baseCode, baseName);
                item.row.ForeignCurrencyDisplay = FormatCurrencyLabel(foreignCode, foreignName);
                payload.Add(item.row);
            }

            return Ok(payload);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.ExchangeRates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null)
            {
                return NotFound();
            }

            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ExchangeRate item)
        {
            if (item == null)
            {
                return BadRequest("Exchange rate data is required.");
            }

            var validationError = await ValidateExchangeRateAsync(item);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            item.IsDeleted = false;
            item.ActionDate = DateTime.UtcNow;
            item.Action = "CREATE";
            item.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.AddAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ExchangeRate item)
        {
            if (item == null)
            {
                return BadRequest("Exchange rate data is required.");
            }

            if (item.Id == 0)
            {
                item.Id = id;
            }
            else if (id != item.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var validationError = await ValidateExchangeRateAsync(item, id);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var existing = await _context.ExchangeRates
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Exchange rate not found.");
            }

            existing.RateDate = item.RateDate.Date;
            existing.BaseCurrencyId = item.BaseCurrencyId;
            existing.ForeignCurrencyId = item.ForeignCurrencyId;
            existing.Rate = item.Rate;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.ExchangeRates
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Exchange rate not found.");
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.ExchangeRates.Update(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static string FormatCurrencyLabel(string code, string name)
        {
            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name))
            {
                return $"{code} - {name}";
            }

            return !string.IsNullOrWhiteSpace(code) ? code : name;
        }

        private async Task<string?> ValidateExchangeRateAsync(ExchangeRate item, int? excludeId = null)
        {
            if (item.RateDate == default)
            {
                return "Date is required.";
            }

            if (item.BaseCurrencyId <= 0)
            {
                return "Base Currency is required.";
            }

            if (item.ForeignCurrencyId <= 0)
            {
                return "Foreign Currency is required.";
            }

            if (item.BaseCurrencyId == item.ForeignCurrencyId)
            {
                return "Base Currency and Foreign Currency must be different.";
            }

            if (item.Rate <= 0)
            {
                return "Rate must be greater than zero.";
            }

            var baseExists = await _context.Currencies
                .AsNoTracking()
                .AnyAsync(c =>
                    c.Id == item.BaseCurrencyId &&
                    (c.IsDeleted == null || c.IsDeleted == false));
            if (!baseExists)
            {
                return "Base Currency not found.";
            }

            var foreignExists = await _context.Currencies
                .AsNoTracking()
                .AnyAsync(c =>
                    c.Id == item.ForeignCurrencyId &&
                    (c.IsDeleted == null || c.IsDeleted == false));
            if (!foreignExists)
            {
                return "Foreign Currency not found.";
            }

            var duplicate = await _context.ExchangeRates
                .AsNoTracking()
                .AnyAsync(x =>
                    x.RateDate == item.RateDate.Date &&
                    x.BaseCurrencyId == item.BaseCurrencyId &&
                    x.ForeignCurrencyId == item.ForeignCurrencyId &&
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    (!excludeId.HasValue || x.Id != excludeId.Value));

            if (duplicate)
            {
                return "An exchange rate for this date and currency pair already exists.";
            }

            return null;
        }
    }
}
