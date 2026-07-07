using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CashAndBanksController : ControllerBase
    {
        private readonly IGenericRepository<CashAndBank> _repository;
        private readonly ApplicationDbContext _context;

        public CashAndBanksController(IGenericRepository<CashAndBank> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 0,
            [FromQuery] int? parentCashAndBankId = null,
            [FromQuery] bool topLevelOnly = false)
        {
            pageNumber = PaginationHelper.NormalizePageNumber(pageNumber);

            var baseQuery = _context.CashAndBanks
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false);

            if (parentCashAndBankId.HasValue && parentCashAndBankId.Value > 0)
            {
                baseQuery = baseQuery.Where(x => x.ParentCashAndBankId == parentCashAndBankId.Value);
            }
            else if (topLevelOnly)
            {
                baseQuery = baseQuery.Where(x => x.ParentCashAndBankId == null);
            }

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = PaginationHelper.FormatPageSizeHeader(pageSize, totalCount);

            var childCountsByParentId = await _context.CashAndBanks
                .AsNoTracking()
                .Where(x =>
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    x.ParentCashAndBankId != null)
                .GroupBy(x => x.ParentCashAndBankId)
                .Select(g => new { ParentId = g.Key!.Value, Count = g.Count() })
                .ToDictionaryAsync(x => x.ParentId, x => x.Count);

            var pageRowsQuery =
                from row in baseQuery
                join coa in _context.ChartOfAccounts.Where(c => c.IsDeleted == null || c.IsDeleted == false)
                    on row.CoaId equals coa.Id into coaGroup
                from coa in coaGroup.DefaultIfEmpty()
                join bank in _context.BankLists.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                    on row.BankListsId equals bank.Id into bankGroup
                from bank in bankGroup.DefaultIfEmpty()
                orderby row.Id descending
                select new
                {
                    row,
                    CoaAcctId = coa != null ? coa.AcctId : string.Empty,
                    CoaAcctName = coa != null ? coa.AcctName : string.Empty,
                    CoaControlAccount = coa != null ? coa.ControlAccount : string.Empty,
                    BankName = bank != null ? bank.Name : string.Empty,
                    BankCode = bank != null ? bank.Code : string.Empty
                };

            var pageRows = await PaginationHelper.ApplyPaging(pageRowsQuery, pageNumber, pageSize)
                .ToListAsync();

            var payload = new List<CashAndBank>(pageRows.Count);
            foreach (var item in pageRows)
            {
                var acctId = (item.CoaAcctId ?? string.Empty).Trim();
                var acctName = (item.CoaAcctName ?? string.Empty).Trim();
                item.row.CoaDisplay = !string.IsNullOrEmpty(acctId) && !string.IsNullOrEmpty(acctName)
                    ? $"{acctId} - {acctName}"
                    : !string.IsNullOrEmpty(acctName)
                        ? acctName
                        : item.CoaControlAccount ?? string.Empty;

                var bankName = (item.BankName ?? string.Empty).Trim();
                var bankCode = (item.BankCode ?? string.Empty).Trim();
                item.row.BankDisplay = !string.IsNullOrEmpty(bankCode)
                    ? $"{bankName} ({bankCode})"
                    : bankName;

                if (item.row.ParentCashAndBankId == null &&
                    childCountsByParentId.TryGetValue(item.row.Id, out var childCount))
                {
                    item.row.ChildCount = childCount;
                }

                payload.Add(item.row);
            }

            return Ok(payload);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.CashAndBanks
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null)
            {
                return NotFound();
            }

            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CashAndBank item)
        {
            if (item == null)
            {
                return BadRequest("Cash and bank data is required.");
            }

            var validationError = ValidateCashAndBank(item);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            item.AcctId = string.IsNullOrWhiteSpace(item.AcctId) ? null : item.AcctId.Trim();
            item.Name = item.Name.Trim();
            item.Currency = item.Currency.Trim();
            item.Mode = item.Mode.Trim();
            item.IBAN = string.IsNullOrWhiteSpace(item.IBAN) ? null : item.IBAN.Trim();
            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "Active" : item.Status.Trim();
            item.ParentCashAndBankId = item.ParentCashAndBankId;
            item.IsDeleted = false;
            item.ActionDate = DateTime.UtcNow;
            item.Action = "CREATE";
            item.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.AddAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CashAndBank item)
        {
            if (item == null)
            {
                return BadRequest("Cash and bank data is required.");
            }

            if (item.Id == 0)
            {
                item.Id = id;
            }
            else if (id != item.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var validationError = ValidateCashAndBank(item);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var existing = await _context.CashAndBanks
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Cash and bank record not found.");
            }

            existing.AcctId = string.IsNullOrWhiteSpace(item.AcctId) ? null : item.AcctId.Trim();
            existing.Name = item.Name.Trim();
            existing.CoaId = item.CoaId;
            existing.Currency = item.Currency.Trim();
            existing.Mode = item.Mode.Trim();
            existing.IBAN = string.IsNullOrWhiteSpace(item.IBAN) ? null : item.IBAN.Trim();
            existing.BankListsId = item.BankListsId;
            existing.Status = string.IsNullOrWhiteSpace(item.Status) ? "Active" : item.Status.Trim();
            existing.ParentCashAndBankId = item.ParentCashAndBankId;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.CashAndBanks
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Cash and bank record not found.");
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.CashAndBanks.Update(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static string? ValidateCashAndBank(CashAndBank item)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                return "Name is required.";
            }

            if (item.CoaId == null || item.CoaId <= 0)
            {
                return "Control Account is required.";
            }

            if (string.IsNullOrWhiteSpace(item.Currency))
            {
                return "Currency is required.";
            }

            if (string.IsNullOrWhiteSpace(item.Mode))
            {
                return "Mode is required.";
            }

            var mode = item.Mode.Trim();
            if (mode != "Cash" && mode != "TR" && mode != "Bank")
            {
                return "Mode must be Cash, TR, or Bank.";
            }

            return null;
        }
    }
}
