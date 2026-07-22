using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BankAccountsController : ControllerBase
    {
        private readonly IGenericRepository<BankAccount> _repository;
        private readonly ApplicationDbContext _context;

        public BankAccountsController(IGenericRepository<BankAccount> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 0,
            CancellationToken cancellationToken = default)
        {
            pageNumber = PaginationHelper.NormalizePageNumber(pageNumber);

            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var (payload, totalCount) = await BankAccountGridQuery.QueryAsync(
                _context,
                scope,
                pageNumber,
                pageSize,
                cancellationToken);

            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = PaginationHelper.FormatPageSizeHeader(pageSize, totalCount);

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                payload.Select(x => x.Id),
                "BankAccounts", "BankAccount");
            var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(payload, x => x.Id, attachedIds);
            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var baseQuery = _context.BankAccounts
                .AsNoTracking()
                .Where(a => a.Id == id && (a.IsDeleted == null || a.IsDeleted == false));
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            var row = await (from acct in baseQuery
                             join rac in _context.AccRacBases
                                 on acct.CmdId equals rac.Id into racGroup
                             from rac in racGroup.DefaultIfEmpty()
                             join un in _context.AccRacBases
                                 on acct.BaseId equals un.Id into unitGroup
                             from un in unitGroup.DefaultIfEmpty()
                             select new
                             {
                                 acct,
                                 CmdName = rac != null ? rac.Name ?? string.Empty : string.Empty,
                                 BaseName = un != null ? un.Name ?? string.Empty : string.Empty
                             })
                .FirstOrDefaultAsync();

            if (row == null)
            {
                return NotFound();
            }

            row.acct.CmdName = row.CmdName;
            row.acct.BaseName = row.BaseName;
            var account = row.acct;

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                new[] { account.Id },
                "BankAccounts", "BankAccount");
            return Ok(AttachmentFlagHelper.ToDictionaryWithAttachmentFlag(account, attachedIds.Contains(account.Id)));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BankAccount item)
        {
            if (item == null)
            {
                return BadRequest("Bank account data is required.");
            }

            item.IsDeleted = false;
            item.CreatedDate ??= DateTime.UtcNow;
            item.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);
            await _repository.AddAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] BankAccount item)
        {
            if (item == null)
            {
                return BadRequest("Bank account data is required.");
            }

            if (item.Id == 0)
            {
                item.Id = id;
            }
            else if (id != item.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == id && (a.IsDeleted == null || a.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Bank account not found.");
            }

            existing.OpeningDate = item.OpeningDate;
            existing.CmdId = item.CmdId;
            existing.BaseId = item.BaseId;
            existing.FundingSource = item.FundingSource;
            existing.FundName = item.FundName;
            existing.TitleOfAccount = item.TitleOfAccount;
            existing.BankName = item.BankName;
            existing.BranchCode = item.BranchCode;
            existing.BranchAddress = item.BranchAddress;
            existing.IBAN = item.IBAN;
            existing.Currency = item.Currency;
            existing.AccountType = item.AccountType;
            existing.SignatoryDate = item.SignatoryDate;
            existing.Signatory1 = item.Signatory1;
            existing.Signatory2 = item.Signatory2;
            existing.Signatory3 = item.Signatory3;
            existing.StatusDate = item.StatusDate;
            existing.Remarks = item.Remarks;
            existing.Authority = item.Authority;
            existing.Reference = item.Reference;
            existing.CreatedDate = item.CreatedDate;
            existing.AccStatus = item.AccStatus;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);
            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromBody] BankAccountDeleteRequest? request = null)
        {
            var account = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == id && (a.IsDeleted == null || a.IsDeleted == false));

            if (account == null)
            {
                return NotFound("Bank account not found.");
            }

            var actionBy = request?.ActionBy;
            if (string.IsNullOrWhiteSpace(actionBy))
            {
                var existingActionBy = await _context.BankAccounts
                    .AsNoTracking()
                    .Where(a => a.Id == id)
                    .Select(a => a.ActionBy)
                    .FirstOrDefaultAsync();
                actionBy = existingActionBy;
            }

            account.IsDeleted = true;
            account.Action = "DELETE";
            account.ActionDate = DateTime.UtcNow;
            account.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, actionBy);

            _context.BankAccounts.Update(account);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class BankAccountDeleteRequest
    {
        public string? ActionBy { get; set; }
    }
}
