using A1.Api.Models;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace A1.Api.Controllers
{
    /// <summary>
    /// Contract invoice schedule (dbo.sp_GetContractInvoiceSchedule) and edits (dbo.ContractInvoicesEdit).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ContractInvoiceScheduleController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ContractInvoiceScheduleController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET: Invoice schedule rows. All query parameters are optional.
        /// Route: GET /api/ContractInvoiceSchedule?contractNo=&amp;fromDate=&amp;toDate=&amp;cmdId=&amp;classId=&amp;baseId=
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? contractNo = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? cmdId = null,
            [FromQuery] int? classId = null,
            [FromQuery] int? baseId = null,
            CancellationToken cancellationToken = default)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            if (!scope.IsAhq)
            {
                if (string.Equals(scope.AccessLevel, "base", StringComparison.OrdinalIgnoreCase))
                {
                    if (!scope.BaseId.HasValue)
                    {
                        return Forbid();
                    }

                    if (baseId.HasValue && baseId.Value != scope.BaseId.Value)
                    {
                        return Forbid();
                    }

                    baseId = scope.BaseId.Value;
                    if (scope.CmdId.HasValue)
                    {
                        if (cmdId.HasValue && cmdId.Value != scope.CmdId.Value)
                        {
                            return Forbid();
                        }

                        cmdId = scope.CmdId.Value;
                    }
                }
                else if (string.Equals(scope.AccessLevel, "command", StringComparison.OrdinalIgnoreCase))
                {
                    if (!scope.CmdId.HasValue)
                    {
                        return Forbid();
                    }

                    if (cmdId.HasValue && cmdId.Value != scope.CmdId.Value)
                    {
                        return Forbid();
                    }

                    cmdId = scope.CmdId.Value;

                    if (scope.BaseId.HasValue)
                    {
                        if (baseId.HasValue && baseId.Value != scope.BaseId.Value)
                        {
                            return Forbid();
                        }

                        baseId = scope.BaseId.Value;
                    }
                    else if (baseId.HasValue && !scope.AllowedBaseIds.Contains(baseId.Value))
                    {
                        return Forbid();
                    }
                }
            }

            await using var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.sp_GetContractInvoiceSchedule";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 120;

            AddStringParameter(command, "@ContractNo", string.IsNullOrWhiteSpace(contractNo) ? null : contractNo.Trim());
            AddDateParameter(command, "@FromDate", fromDate);
            AddDateParameter(command, "@ToDate", toDate);
            AddIntParameter(command, "@CmdId", cmdId);
            AddIntParameter(command, "@ClassId", classId);
            AddIntParameter(command, "@BaseId", baseId);

            var results = new List<Dictionary<string, object?>>();

            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection,
                cancellationToken);

            var fieldCount = reader.FieldCount;
            var names = new string[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                names[i] = reader.GetName(i);
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[names[i]] = value == DBNull.Value ? null : value;
                }

                results.Add(row);
            }

            return Ok(results);
        }

        /// <summary>
        /// GET: All non-deleted invoice edits for an InvoiceNo.
        /// Route: GET /api/ContractInvoiceSchedule/by-invoice/{invoiceNo}
        /// </summary>
        [HttpGet("by-invoice/{invoiceNo}")]
        public async Task<IActionResult> GetByInvoiceNo(string invoiceNo)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo))
            {
                return BadRequest("invoiceNo is required.");
            }

            invoiceNo = invoiceNo.Trim();

            var items = await _context.ContractInvoicesEdits
                .AsNoTracking()
                .Where(x =>
                    (x.InvoiceNo ?? string.Empty) == invoiceNo
                    && x.SubInvoiceNo != null
                    && (x.IsDeleted == null || x.IsDeleted == false))
                .OrderByDescending(x => x.Id)
                .Select(x => new ContractInvoicesEdit
                {
                    Id = x.Id,
                    ActionDate = x.ActionDate,
                    ActionBy = x.ActionBy,
                    Action = x.Action,
                    IsDeleted = x.IsDeleted,
                    ContractId = x.ContractId,
                    ContractNo = x.ContractNo ?? string.Empty,
                    InvoiceNo = x.InvoiceNo ?? string.Empty,
                    SubInvoiceNo = x.SubInvoiceNo ?? string.Empty,
                    PeriodStart = x.PeriodStart,
                    PeriodEnd = x.PeriodEnd,
                    DueDate = x.DueDate,
                    Months = x.Months,
                    CalculatedRentPM = x.CalculatedRentPM,
                    TotalRent = x.TotalRent,
                    Remarks = x.Remarks,
                    ContractStartDate = x.ContractStartDate,
                    ContractEndDate = x.ContractEndDate,
                    ContractPeriod = x.ContractPeriod,
                    BusinessName = x.BusinessName,
                    InitialRentPM = x.InitialRentPM,
                    PaymentTermMonths = x.PaymentTermMonths,
                    RiseTermType = x.RiseTermType,
                    RiseTerm = x.RiseTerm,
                    RiseRate = x.RiseRate,
                    RiseDate = x.RiseDate,
                    InvoiceDateType = x.InvoiceDateType,
                    CmdId = x.CmdId,
                    BaseId = x.BaseId,
                    ClassId = x.ClassId,
                    AmountReceived = x.AmountReceived,
                    AmountReceivable = x.AmountReceivable,
                    AmountPending = x.AmountPending,
                    InvoiceStatus = x.InvoiceStatus,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>
        /// POST: Create invoice edit row. Fails if ContractNo + InvoiceNo + SubInvoiceNo already exists.
        /// Route: POST /api/ContractInvoiceSchedule/{contractNo}/{invoiceNo}/{subInvoiceNo}
        /// </summary>
        [HttpPost("{contractNo}/{invoiceNo}/{subInvoiceNo}")]
        public async Task<IActionResult> Create(
            string contractNo,
            string invoiceNo,
            string subInvoiceNo,
            [FromBody] ContractInvoicesEdit model)
        {
            if (!TryNormalizeKeys(contractNo, invoiceNo, subInvoiceNo, out contractNo, out invoiceNo, out subInvoiceNo, out var keyError))
            {
                return BadRequest(keyError);
            }

            if (model == null)
            {
                return BadRequest("Invoice data is required.");
            }

            var invoiceDate = model.DueDate ?? model.PeriodEnd;
            if (await IsInvoiceDateLockedAsync(invoiceDate))
            {
                return BadRequest("Date Locked");
            }

            var existing = await FindEditAsync(contractNo, invoiceNo, subInvoiceNo);
            if (existing != null && (existing.IsDeleted == null || existing.IsDeleted == false))
            {
                return Conflict("An invoice edit with this contractNo, invoiceNo, and subInvoiceNo already exists.");
            }

            var scopeError = await ValidateScopeAsync(model, existing);
            if (scopeError != null)
            {
                return scopeError;
            }

            if (existing != null)
            {
                existing.IsDeleted = false;
                existing.ContractNo = contractNo;
                existing.InvoiceNo = invoiceNo;
                existing.SubInvoiceNo = subInvoiceNo;
                ApplyInvoiceModel(existing, model, isCreate: true);
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            var created = new ContractInvoicesEdit
            {
                ContractNo = contractNo,
                InvoiceNo = invoiceNo,
                SubInvoiceNo = subInvoiceNo,
                CreatedAt = DateTime.UtcNow
            };
            ApplyInvoiceModel(created, model, isCreate: true);
            await _context.ContractInvoicesEdits.AddAsync(created);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetByInvoiceNo), new { invoiceNo }, created);
        }

        /// <summary>
        /// PUT: Upsert by ContractNo, InvoiceNo, and SubInvoiceNo — update if exists, otherwise create.
        /// Route: PUT /api/ContractInvoiceSchedule/{contractNo}/{invoiceNo}/{subInvoiceNo}
        /// </summary>
        [HttpPut("{contractNo}/{invoiceNo}/{subInvoiceNo}")]
        public async Task<IActionResult> Update(
            string contractNo,
            string invoiceNo,
            string subInvoiceNo,
            [FromBody] ContractInvoicesEdit model)
        {
            if (!TryNormalizeKeys(contractNo, invoiceNo, subInvoiceNo, out contractNo, out invoiceNo, out subInvoiceNo, out var keyError))
            {
                return BadRequest(keyError);
            }

            if (model == null)
            {
                return BadRequest("Invoice data is required.");
            }

            var existing = await FindEditAsync(contractNo, invoiceNo, subInvoiceNo);

            var invoiceDate = model.DueDate ?? model.PeriodEnd;
            if (await IsInvoiceDateLockedAsync(invoiceDate))
            {
                return BadRequest("Date Locked");
            }

            var scopeError = await ValidateScopeAsync(model, existing);
            if (scopeError != null)
            {
                return scopeError;
            }

            if (existing != null)
            {
                existing.ContractNo = contractNo;
                existing.InvoiceNo = invoiceNo;
                existing.SubInvoiceNo = subInvoiceNo;
                ApplyInvoiceModel(existing, model, isCreate: false);
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            var created = new ContractInvoicesEdit
            {
                ContractNo = contractNo,
                InvoiceNo = invoiceNo,
                SubInvoiceNo = subInvoiceNo,
                CreatedAt = DateTime.UtcNow
            };
            ApplyInvoiceModel(created, model, isCreate: true);
            await _context.ContractInvoicesEdits.AddAsync(created);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetByInvoiceNo), new { invoiceNo }, created);
        }

        /// <summary>
        /// DELETE: Soft delete by ContractNo, InvoiceNo, and SubInvoiceNo.
        /// Route: DELETE /api/ContractInvoiceSchedule/{contractNo}/{invoiceNo}/{subInvoiceNo}
        /// </summary>
        [HttpDelete("{contractNo}/{invoiceNo}/{subInvoiceNo}")]
        public async Task<IActionResult> Delete(
            string contractNo,
            string invoiceNo,
            string subInvoiceNo)
        {
            if (!TryNormalizeKeys(contractNo, invoiceNo, subInvoiceNo, out contractNo, out invoiceNo, out subInvoiceNo, out var keyError))
            {
                return BadRequest(keyError);
            }

            var existing = await FindEditAsync(contractNo, invoiceNo, subInvoiceNo);
            if (existing == null || existing.IsDeleted == true)
            {
                return NotFound("Invoice edit not found.");
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<ContractInvoicesEdit?> FindEditAsync(string contractNo, string invoiceNo, string subInvoiceNo)
        {
            return await _context.ContractInvoicesEdits
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.ContractNo == contractNo
                    && x.InvoiceNo == invoiceNo
                    && (x.SubInvoiceNo ?? string.Empty) == subInvoiceNo);
        }

        private static bool TryNormalizeKeys(
            string contractNo,
            string invoiceNo,
            string subInvoiceNo,
            out string normalizedContractNo,
            out string normalizedInvoiceNo,
            out string normalizedSubInvoiceNo,
            out string error)
        {
            normalizedContractNo = contractNo.Trim();
            normalizedInvoiceNo = invoiceNo.Trim();
            normalizedSubInvoiceNo = subInvoiceNo.Trim();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedContractNo)
                || string.IsNullOrWhiteSpace(normalizedInvoiceNo)
                || string.IsNullOrWhiteSpace(normalizedSubInvoiceNo))
            {
                error = "contractNo, invoiceNo, and subInvoiceNo are required.";
                return false;
            }

            return true;
        }

        private async Task<IActionResult?> ValidateScopeAsync(ContractInvoicesEdit model, ContractInvoicesEdit? existing)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            if (scope.IsAhq)
            {
                return null;
            }

            var cmdId = model.CmdId ?? existing?.CmdId;
            var baseId = model.BaseId ?? existing?.BaseId;

            if (string.Equals(scope.AccessLevel, "base", StringComparison.OrdinalIgnoreCase))
            {
                if (!scope.BaseId.HasValue || (baseId.HasValue && baseId.Value != scope.BaseId.Value))
                {
                    return Forbid();
                }
            }
            else if (string.Equals(scope.AccessLevel, "command", StringComparison.OrdinalIgnoreCase))
            {
                if (!scope.CmdId.HasValue || (cmdId.HasValue && cmdId.Value != scope.CmdId.Value))
                {
                    return Forbid();
                }

                if (scope.BaseId.HasValue)
                {
                    if (baseId.HasValue && baseId.Value != scope.BaseId.Value)
                    {
                        return Forbid();
                    }
                }
                else if (baseId.HasValue && !scope.AllowedBaseIds.Contains(baseId.Value))
                {
                    return Forbid();
                }
            }

            return null;
        }

        /// <summary>
        /// Returns true when create/update must be blocked (invoice date is not strictly after the lock date).
        /// </summary>
        private async Task<bool> IsInvoiceDateLockedAsync(DateTime? invoiceDate)
        {
            if (!invoiceDate.HasValue || invoiceDate.Value == default)
            {
                return true;
            }

            var lockingDate = await _context.LockDates
                .AsNoTracking()
                .Where(x => x.LockingDate != null)
                .OrderByDescending(x => x.Id)
                .Select(x => x.LockingDate)
                .FirstOrDefaultAsync();

            if (!lockingDate.HasValue)
            {
                return false;
            }

            return invoiceDate.Value.Date <= lockingDate.Value.Date;
        }

        private void ApplyInvoiceModel(ContractInvoicesEdit target, ContractInvoicesEdit source, bool isCreate)
        {
            target.ContractId = source.ContractId;
            target.SubInvoiceNo = string.IsNullOrWhiteSpace(source.SubInvoiceNo)
                ? target.SubInvoiceNo
                : source.SubInvoiceNo.Trim();
            target.PeriodStart = source.PeriodStart;
            target.PeriodEnd = source.PeriodEnd;
            target.DueDate = source.DueDate;
            target.Months = source.Months;
            target.CalculatedRentPM = source.CalculatedRentPM;
            target.TotalRent = source.TotalRent;
            target.Remarks = source.Remarks;
            target.ContractStartDate = source.ContractStartDate;
            target.ContractEndDate = source.ContractEndDate;
            target.ContractPeriod = source.ContractPeriod;
            target.BusinessName = source.BusinessName;
            target.InitialRentPM = source.InitialRentPM;
            target.PaymentTermMonths = source.PaymentTermMonths;
            target.RiseTermType = source.RiseTermType;
            target.RiseTerm = source.RiseTerm;
            target.RiseRate = source.RiseRate;
            target.RiseDate = source.RiseDate;
            target.InvoiceDateType = source.InvoiceDateType;
            target.CmdId = source.CmdId;
            target.BaseId = source.BaseId;
            target.ClassId = source.ClassId;
            target.AmountReceived = source.AmountReceived;
            target.AmountReceivable = source.AmountReceivable;
            target.AmountPending = source.AmountPending;
            target.InvoiceStatus = source.InvoiceStatus;

            if (isCreate)
            {
                target.IsDeleted = source.IsDeleted ?? false;
            }

            target.ActionDate = source.ActionDate ?? DateTime.UtcNow;
            target.Action = string.IsNullOrWhiteSpace(source.Action)
                ? (isCreate ? "CREATE" : "UPDATE")
                : source.Action;
            target.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, source.ActionBy);
        }

        private static void AddStringParameter(DbCommand command, string name, string? value)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.DbType = DbType.String;
            p.Size = 50;
            p.Value = value == null ? DBNull.Value : value;
            command.Parameters.Add(p);
        }

        private static void AddDateParameter(DbCommand command, string name, DateTime? value)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.DbType = DbType.Date;
            p.Value = value.HasValue ? value.Value.Date : DBNull.Value;
            command.Parameters.Add(p);
        }

        private static void AddIntParameter(DbCommand command, string name, int? value)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.DbType = DbType.Int32;
            p.Value = value.HasValue ? value.Value : DBNull.Value;
            command.Parameters.Add(p);
        }
    }
}
