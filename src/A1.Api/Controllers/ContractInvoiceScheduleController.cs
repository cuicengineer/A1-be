using A1.Api.Models;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Security.Claims;

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
            [FromQuery] bool? isFinalized = null,
            [FromQuery] string? invoiceNo = null,
            CancellationToken cancellationToken = default)
        {
            var scope = await ApplyScheduleScopeFiltersAsync(cmdId, baseId);
            if (scope.Error != null)
            {
                return scope.Error;
            }

            var results = await QueryContractInvoiceScheduleAsync(
                contractNo,
                fromDate,
                toDate,
                scope.CmdId,
                classId,
                scope.BaseId,
                cancellationToken);

            return Ok(FilterScheduleRows(results, isFinalized, invoiceNo));
        }

        /// <summary>
        /// GET: Finalized invoice schedule search (agreement-prov-invoice). All query parameters optional.
        /// Route: GET /api/ContractInvoiceSchedule/by-invoice?invoiceNo=&amp;contractNo=&amp;fromDate=&amp;toDate=&amp;cmdId=&amp;classId=&amp;baseId=&amp;isFinalized=
        /// </summary>
        [HttpGet("by-invoice")]
        public async Task<IActionResult> SearchByInvoiceQuery(
            [FromQuery] string? invoiceNo = null,
            [FromQuery] string? contractNo = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? cmdId = null,
            [FromQuery] int? classId = null,
            [FromQuery] int? baseId = null,
            [FromQuery] bool? isFinalized = true,
            CancellationToken cancellationToken = default)
        {
            var scope = await ApplyScheduleScopeFiltersAsync(cmdId, baseId);
            if (scope.Error != null)
            {
                return scope.Error;
            }

            var results = await QueryContractInvoiceScheduleAsync(
                contractNo,
                fromDate,
                toDate,
                scope.CmdId,
                classId,
                scope.BaseId,
                cancellationToken);

            return Ok(FilterScheduleRows(results, isFinalized, invoiceNo));
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
                    ItemwithCode = x.ItemwithCode,
                    Description = x.Description,
                    AccHead = x.AccHead,
                    Discount = x.Discount,
                    SortOrder = x.SortOrder,
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

            var lockedError = await GetLockedInvoiceEditErrorAsync(contractNo, invoiceNo, subInvoiceNo, existing, model);
            if (lockedError != null)
            {
                return BadRequest(lockedError);
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
        [HttpPut("{contractNo}/{invoiceNo}/{subInvoiceNo?}")]
        public async Task<IActionResult> Update(
            string contractNo,
            string invoiceNo,
            string? subInvoiceNo,
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

            if (IsInvoiceUnlocking(existing, model) && !await CanPrivilegedDeleteInvoiceAsync())
            {
                return Forbid();
            }

            var lockedError = await GetLockedInvoiceEditErrorAsync(contractNo, invoiceNo, subInvoiceNo, existing, model);
            if (lockedError != null)
            {
                return BadRequest(lockedError);
            }

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
        /// DELETE: Soft delete all invoice edit rows (header + line items) for ContractNo + InvoiceNo.
        /// Route: DELETE /api/ContractInvoiceSchedule/{contractNo}/{invoiceNo}
        /// Restricted to superuser or AHQ supervisor.
        /// </summary>
        [HttpDelete("{contractNo}/{invoiceNo}")]
        public async Task<IActionResult> DeleteInvoice(
            string contractNo,
            string invoiceNo,
            [FromBody] ContractInvoiceDeleteRequest? request = null)
        {
            if (!await CanPrivilegedDeleteInvoiceAsync())
            {
                return Forbid();
            }

            contractNo = contractNo?.Trim() ?? string.Empty;
            invoiceNo = invoiceNo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(contractNo) || string.IsNullOrWhiteSpace(invoiceNo))
            {
                return BadRequest("contractNo and invoiceNo are required.");
            }

            var rows = await _context.ContractInvoicesEdits
                .Where(x =>
                    x.ContractNo == contractNo
                    && x.InvoiceNo == invoiceNo
                    && (x.IsDeleted == null || x.IsDeleted == false))
                .ToListAsync();

            if (rows.Count == 0)
            {
                return NotFound("Invoice not found.");
            }

            var headerRow = rows.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.SubInvoiceNo));
            if (headerRow?.IsLocked == true)
            {
                return BadRequest("Invoice is locked. Unlock it before deleting.");
            }

            var actionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, request?.ActionBy);
            var actionDate = DateTime.UtcNow;
            foreach (var row in rows)
            {
                row.IsDeleted = true;
                row.Action = "DELETE";
                row.ActionDate = actionDate;
                row.ActionBy = actionBy;
            }

            await _context.SaveChangesAsync();
            return NoContent();
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

            var lockedError = await GetLockedInvoiceEditErrorAsync(contractNo, invoiceNo, subInvoiceNo, existing, null);
            if (lockedError != null)
            {
                return BadRequest(lockedError);
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<ContractInvoicesEdit?> FindEditAsync(
            string contractNo,
            string invoiceNo,
            string? subInvoiceNo)
        {
            var isHeaderRow = string.IsNullOrWhiteSpace(subInvoiceNo);
            return await _context.ContractInvoicesEdits
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.ContractNo == contractNo
                    && x.InvoiceNo == invoiceNo
                    && (isHeaderRow
                        ? x.SubInvoiceNo == null || x.SubInvoiceNo == ""
                        : (x.SubInvoiceNo ?? string.Empty) == subInvoiceNo));
        }

        private static bool TryNormalizeKeys(
            string contractNo,
            string invoiceNo,
            string? subInvoiceNo,
            out string normalizedContractNo,
            out string normalizedInvoiceNo,
            out string? normalizedSubInvoiceNo,
            out string error)
        {
            normalizedContractNo = contractNo?.Trim() ?? string.Empty;
            normalizedInvoiceNo = invoiceNo?.Trim() ?? string.Empty;
            var subTrimmed = subInvoiceNo?.Trim();
            normalizedSubInvoiceNo = string.IsNullOrWhiteSpace(subTrimmed) ? null : subTrimmed;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedContractNo)
                || string.IsNullOrWhiteSpace(normalizedInvoiceNo))
            {
                error = "contractNo and invoiceNo are required.";
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
            if (!string.IsNullOrWhiteSpace(source.SubInvoiceNo))
            {
                target.SubInvoiceNo = source.SubInvoiceNo.Trim();
            }
            else if (string.IsNullOrWhiteSpace(target.SubInvoiceNo))
            {
                target.SubInvoiceNo = null;
            }
            target.PeriodStart = source.PeriodStart;
            target.PeriodEnd = source.PeriodEnd;
            target.DueDate = source.DueDate;
            target.Months = source.Months;
            target.CalculatedRentPM = source.CalculatedRentPM;
            target.TotalRent = source.TotalRent;
            target.ItemwithCode = source.ItemwithCode;
            target.Description = source.Description;
            target.AccHead = source.AccHead;
            target.Discount = source.Discount;
            target.SortOrder = source.SortOrder;
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
            target.IsLocked = source.IsLocked;
            if (source.IsFinalized.HasValue)
            {
                target.IsFinalized = source.IsFinalized;
            }

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

        private async Task<(IActionResult? Error, int? CmdId, int? BaseId)> ApplyScheduleScopeFiltersAsync(
            int? cmdId,
            int? baseId)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            if (scope.IsAhq)
            {
                return (null, cmdId, baseId);
            }

            if (string.Equals(scope.AccessLevel, "base", StringComparison.OrdinalIgnoreCase))
            {
                if (!scope.BaseId.HasValue)
                {
                    return (Forbid(), cmdId, baseId);
                }

                if (baseId.HasValue && baseId.Value != scope.BaseId.Value)
                {
                    return (Forbid(), cmdId, baseId);
                }

                baseId = scope.BaseId.Value;
                if (scope.CmdId.HasValue)
                {
                    if (cmdId.HasValue && cmdId.Value != scope.CmdId.Value)
                    {
                        return (Forbid(), cmdId, baseId);
                    }

                    cmdId = scope.CmdId.Value;
                }
            }
            else if (string.Equals(scope.AccessLevel, "command", StringComparison.OrdinalIgnoreCase))
            {
                if (!scope.CmdId.HasValue)
                {
                    return (Forbid(), cmdId, baseId);
                }

                if (cmdId.HasValue && cmdId.Value != scope.CmdId.Value)
                {
                    return (Forbid(), cmdId, baseId);
                }

                cmdId = scope.CmdId.Value;

                if (scope.BaseId.HasValue)
                {
                    if (baseId.HasValue && baseId.Value != scope.BaseId.Value)
                    {
                        return (Forbid(), cmdId, baseId);
                    }

                    baseId = scope.BaseId.Value;
                }
                else if (baseId.HasValue && !scope.AllowedBaseIds.Contains(baseId.Value))
                {
                    return (Forbid(), cmdId, baseId);
                }
            }

            return (null, cmdId, baseId);
        }

        private async Task<List<Dictionary<string, object?>>> QueryContractInvoiceScheduleAsync(
            string? contractNo,
            DateTime? fromDate,
            DateTime? toDate,
            int? cmdId,
            int? classId,
            int? baseId,
            CancellationToken cancellationToken)
        {
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

            return results;
        }

        private static List<Dictionary<string, object?>> FilterScheduleRows(
            IEnumerable<Dictionary<string, object?>> rows,
            bool? isFinalized,
            string? invoiceNo)
        {
            IEnumerable<Dictionary<string, object?>> filtered = rows;

            if (isFinalized == true)
            {
                filtered = filtered.Where(RowIsFinalized);
            }
            else if (isFinalized == false)
            {
                filtered = filtered.Where(row => !RowIsFinalized(row));
            }

            if (!string.IsNullOrWhiteSpace(invoiceNo))
            {
                var invoiceKey = invoiceNo.Trim();
                filtered = filtered.Where(row =>
                    string.Equals(ReadRowString(row, "InvoiceNo"), invoiceKey, StringComparison.OrdinalIgnoreCase));
            }

            return filtered.ToList();
        }

        private static bool RowIsFinalized(Dictionary<string, object?> row)
        {
            if (!TryReadRowValue(row, "IsFinalized", out var value)
                && !TryReadRowValue(row, "isFinalized", out value)
                && !TryReadRowValue(row, "IsFinalize", out value)
                && !TryReadRowValue(row, "isFinalize", out value))
            {
                return false;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is byte or sbyte or short or ushort or int or uint or long or ulong)
            {
                return Convert.ToInt64(value) == 1;
            }

            var text = Convert.ToString(value)?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return long.TryParse(text, out var numeric) && numeric == 1;
        }

        private static string ReadRowString(Dictionary<string, object?> row, string key)
        {
            return TryReadRowValue(row, key, out var value)
                ? Convert.ToString(value)?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static bool TryReadRowValue(Dictionary<string, object?> row, string key, out object? value)
        {
            if (row.TryGetValue(key, out value))
            {
                return true;
            }

            foreach (var pair in row)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null;
            return false;
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

        private async Task<string?> GetLockedInvoiceEditErrorAsync(
            string contractNo,
            string invoiceNo,
            string? subInvoiceNo,
            ContractInvoicesEdit? existing,
            ContractInvoicesEdit? source)
        {
            var header = await FindHeaderEditAsync(contractNo, invoiceNo);
            if (header?.IsLocked != true)
            {
                return null;
            }

            var isHeaderRow = string.IsNullOrWhiteSpace(subInvoiceNo);
            if (isHeaderRow && source != null && IsInvoiceUnlocking(existing, source))
            {
                return null;
            }

            return "Invoice is locked. Unlock it before editing.";
        }

        private async Task<ContractInvoicesEdit?> FindHeaderEditAsync(string contractNo, string invoiceNo)
        {
            return await _context.ContractInvoicesEdits
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.ContractNo == contractNo
                    && x.InvoiceNo == invoiceNo
                    && (x.SubInvoiceNo == null || x.SubInvoiceNo == "")
                    && (x.IsDeleted == null || x.IsDeleted == false));
        }

        private static bool IsInvoiceUnlocking(ContractInvoicesEdit? existing, ContractInvoicesEdit source)
        {
            if (existing == null || existing.IsLocked != true)
            {
                return false;
            }

            return source.IsLocked != true;
        }

        private static bool IsInvoiceLockStateChanging(ContractInvoicesEdit? existing, ContractInvoicesEdit source)
        {
            var nextLocked = source.IsLocked == true;
            if (existing == null)
            {
                return nextLocked;
            }

            return (existing.IsLocked == true) != nextLocked;
        }

        private static bool IsLoginSuperuser(ClaimsPrincipal user)
        {
            var loginName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity?.Name;
            return string.Equals(loginName?.Trim(), "superuser", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CanPrivilegedDeleteInvoiceAsync()
        {
            if (IsLoginSuperuser(User))
            {
                return true;
            }

            return await DataAccessScopeHelper.IsAhqSupervisorAsync(User, _context);
        }
    }

    public class ContractInvoiceDeleteRequest
    {
        public string? ActionBy { get; set; }
    }
}
