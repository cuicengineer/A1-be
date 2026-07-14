using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InterAccTransfersController : ControllerBase
    {
        private readonly IGenericRepository<InterAccTransfer> _repository;
        private readonly ApplicationDbContext _context;

        public InterAccTransfersController(
            IGenericRepository<InterAccTransfer> repository,
            ApplicationDbContext context)
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

            var (payload, totalCount) = await InterAccTransferGridQuery.QueryAsync(
                _context,
                pageNumber,
                pageSize,
                cancellationToken);

            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = PaginationHelper.FormatPageSizeHeader(pageSize, totalCount);

            return Ok(payload);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.InterAccTransfers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null)
            {
                return NotFound();
            }

            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] InterAccTransfer item)
        {
            if (item == null)
            {
                return BadRequest("Inter account transfer data is required.");
            }

            var validationError = await ValidateInterAccTransferAsync(item);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var receivedInAccount = await _context.CashAndBanks
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.Id == item.ReceivedInAccountId &&
                    (a.IsDeleted == null || a.IsDeleted == false));

            if (receivedInAccount != null && IsTrMode(receivedInAccount.Mode))
            {
                var particulars = (item.Particulars ?? string.Empty).Trim();
                var creationDate = item.TransferDate != default ? item.TransferDate.Date : DateTime.UtcNow.Date;
                var childAcctId = await CashAndBankTrChildHelper.GenerateTrChildAcctIdAsync(
                    _context,
                    receivedInAccount,
                    creationDate);

                var trLedger = new CashAndBank
                {
                    AcctId = childAcctId,
                    Name = $"TR - {particulars}",
                    CoaId = receivedInAccount.CoaId,
                    Currency = receivedInAccount.Currency,
                    Mode = "TR",
                    IBAN = receivedInAccount.IBAN,
                    BankListsId = receivedInAccount.BankListsId,
                    Status = "Active",
                    ParentCashAndBankId = receivedInAccount.Id,
                    IsDeleted = false,
                    ActionDate = DateTime.UtcNow,
                    Action = "CREATE",
                    ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy)
                };

                _context.CashAndBanks.Add(trLedger);
                await _context.SaveChangesAsync();
                item.ReceivedInAccountId = trLedger.Id;
            }

            NormalizeInterAccTransfer(item);
            item.IsDeleted = false;
            item.ActionDate = DateTime.UtcNow;
            item.Action = "CREATE";
            item.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.AddAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] InterAccTransfer item)
        {
            if (item == null)
            {
                return BadRequest("Inter account transfer data is required.");
            }

            if (!await CanManageInterAccTransferAsync())
            {
                return BadRequest("Only superuser or AHQ supervisor can edit inter account transfers.");
            }

            if (item.Id == 0)
            {
                item.Id = id;
            }
            else if (id != item.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var validationError = await ValidateInterAccTransferAsync(item, id);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var existing = await _context.InterAccTransfers
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Inter account transfer not found.");
            }

            NormalizeInterAccTransfer(item);

            if (IsLocked(existing.Status))
            {
                if (!IsLocked(item.Status))
                {
                    existing.Status = item.Status;
                    existing.ActionDate = DateTime.UtcNow;
                    existing.Action = "UPDATE";
                    existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

                    await _repository.UpdateAsync(existing);
                    return NoContent();
                }

                return BadRequest("Locked transfers cannot be edited.");
            }

            existing.TransferDate = item.TransferDate.Date;
            existing.VrNo = item.VrNo;
            existing.Description = item.Description;
            existing.Particulars = item.Particulars;
            existing.PaidFromAccountId = item.PaidFromAccountId;
            existing.SettlementVrNo = item.SettlementVrNo;
            existing.PaidFromAmount = item.PaidFromAmount;
            existing.ReceivedInAccountId = item.ReceivedInAccountId;
            existing.ReceivedInAmount = item.ReceivedInAmount;
            existing.TinFtn = item.TinFtn;
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
            if (!await CanManageInterAccTransferAsync())
            {
                return BadRequest("Only superuser or AHQ supervisor can delete inter account transfers.");
            }

            var existing = await _context.InterAccTransfers
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Inter account transfer not found.");
            }

            if (IsLocked(existing.Status))
            {
                return BadRequest("Locked transfers cannot be deleted.");
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.InterAccTransfers.Update(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static bool IsTrMode(string? mode)
        {
            return string.Equals(mode?.Trim(), "TR", StringComparison.OrdinalIgnoreCase);
        }

        private static void NormalizeInterAccTransfer(InterAccTransfer item)
        {
            item.TransferDate = item.TransferDate.Date;
            item.VrNo = (item.VrNo ?? string.Empty).Trim();
            item.Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim();
            if (item.Description != null && item.Description.Length > 35)
            {
                item.Description = item.Description.Substring(0, 35);
            }
            item.Particulars = string.IsNullOrWhiteSpace(item.Particulars) ? null : item.Particulars.Trim();
            item.SettlementVrNo = string.IsNullOrWhiteSpace(item.SettlementVrNo) ? null : item.SettlementVrNo.Trim();
            item.TinFtn = string.IsNullOrWhiteSpace(item.TinFtn) ? null : item.TinFtn.Trim();
            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "Unlock" : item.Status.Trim();
        }

        private static bool IsLocked(string? status)
        {
            return string.Equals(status?.Trim(), "Lock", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string?> ValidateInterAccTransferAsync(InterAccTransfer item, int? excludeId = null)
        {
            if (item.TransferDate == default)
            {
                return "Date is required.";
            }

            if (string.IsNullOrWhiteSpace(item.VrNo))
            {
                return "Vr No is required.";
            }

            if (item.PaidFromAccountId <= 0)
            {
                return "Paid From Account is required.";
            }

            if (item.ReceivedInAccountId <= 0)
            {
                return "Received in Account is required.";
            }

            if (item.PaidFromAccountId == item.ReceivedInAccountId)
            {
                return "Paid From Account and Received in Account must be different.";
            }

            if (item.PaidFromAmount <= 0)
            {
                return "Paid From Amount must be greater than zero.";
            }

            if (item.ReceivedInAmount <= 0)
            {
                return "Received in Amount must be greater than zero.";
            }

            var status = string.IsNullOrWhiteSpace(item.Status) ? "Unlock" : item.Status.Trim();
            if (!string.Equals(status, "Lock", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, "Unlock", StringComparison.OrdinalIgnoreCase))
            {
                return "Status must be Lock or Unlock.";
            }

            var paidFromValid = await IsActiveCashAndBankAccountAsync(item.PaidFromAccountId);
            if (!paidFromValid)
            {
                return "Paid From Account not found or inactive.";
            }

            var receivedInValid = await IsActiveCashAndBankAccountAsync(item.ReceivedInAccountId);
            if (!receivedInValid)
            {
                return "Received in Account not found or inactive.";
            }

            var paidFromAccount = await _context.CashAndBanks
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.Id == item.PaidFromAccountId &&
                    (a.IsDeleted == null || a.IsDeleted == false));

            var receivedInAccount = await _context.CashAndBanks
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.Id == item.ReceivedInAccountId &&
                    (a.IsDeleted == null || a.IsDeleted == false));

            if (paidFromAccount != null &&
                receivedInAccount != null &&
                !string.Equals(
                    (paidFromAccount.Currency ?? string.Empty).Trim(),
                    (receivedInAccount.Currency ?? string.Empty).Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Paid From Account and Received in Account must use the same currency.";
            }

            var paidFromIsTr = paidFromAccount != null && IsTrMode(paidFromAccount.Mode);
            var receivedInIsTr = receivedInAccount != null && IsTrMode(receivedInAccount.Mode);
            if ((paidFromIsTr || receivedInIsTr) && string.IsNullOrWhiteSpace(item.Particulars))
            {
                return "Particulars is required when a TR mode account is selected.";
            }

            if (!string.IsNullOrWhiteSpace(item.Description) && item.Description.Trim().Length > 35)
            {
                return "Description cannot exceed 35 characters.";
            }

            var vrNo = item.VrNo.Trim();
            var duplicate = await _context.InterAccTransfers
                .AsNoTracking()
                .AnyAsync(x =>
                    x.VrNo == vrNo &&
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    (!excludeId.HasValue || x.Id != excludeId.Value));

            if (duplicate)
            {
                return "A transfer with this Vr No already exists.";
            }

            return null;
        }

        private async Task<bool> IsActiveCashAndBankAccountAsync(int accountId)
        {
            return await _context.CashAndBanks
                .AsNoTracking()
                .AnyAsync(a =>
                    a.Id == accountId &&
                    (a.IsDeleted == null || a.IsDeleted == false) &&
                    (a.Status == null ||
                     a.Status == "" ||
                     a.Status == "Active" ||
                     a.Status.ToLower() != "inactive"));
        }

        private static bool IsLoginSuperuser(ClaimsPrincipal user)
        {
            var loginName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity?.Name;
            return string.Equals(loginName?.Trim(), "superuser", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CanManageInterAccTransferAsync()
        {
            if (IsLoginSuperuser(User))
            {
                return true;
            }

            return await DataAccessScopeHelper.IsAhqSupervisorAsync(User, _context);
        }
    }
}
