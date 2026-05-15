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
    /// GET — optional filters; result rows returned as-is from the procedure.
    /// PUT — update invoice row by contractNo and invoiceNo.
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
        /// PUT: Upsert by ContractNo and InvoiceNo — update if exists, otherwise create.
        /// Route: PUT /api/ContractInvoiceSchedule/{contractNo}/{invoiceNo}
        /// </summary>
        [HttpPut("{contractNo}/{invoiceNo}")]
        public async Task<IActionResult> Update(
            string contractNo,
            string invoiceNo,
            [FromBody] ContractInvoicesEdit model)
        {
            if (string.IsNullOrWhiteSpace(contractNo) || string.IsNullOrWhiteSpace(invoiceNo))
            {
                return BadRequest("contractNo and invoiceNo are required.");
            }

            if (model == null)
            {
                return BadRequest("Invoice data is required.");
            }

            contractNo = contractNo.Trim();
            invoiceNo = invoiceNo.Trim();

            var existing = await _context.ContractInvoicesEdits
                .FirstOrDefaultAsync(x => x.ContractNo == contractNo && x.InvoiceNo == invoiceNo);

            if (await IsPeriodEndLockedAsync(model.PeriodEnd))
            {
                return BadRequest("Date Locked");
            }

            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            if (!scope.IsAhq)
            {
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
            }

            if (existing != null)
            {
                ApplyInvoiceModel(existing, model);
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            var created = new ContractInvoicesEdit
            {
                ContractNo = contractNo,
                InvoiceNo = invoiceNo,
                CreatedAt = DateTime.UtcNow
            };
            ApplyInvoiceModel(created, model);
            await _context.ContractInvoicesEdits.AddAsync(created);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Update), new { contractNo, invoiceNo }, created);
        }

        private async Task<bool> IsPeriodEndLockedAsync(DateTime periodEnd)
        {
            if (periodEnd == default)
            {
                return false;
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

            return periodEnd.Date >= lockingDate.Value.Date;
        }

        private static void ApplyInvoiceModel(ContractInvoicesEdit target, ContractInvoicesEdit source)
        {
            target.ContractId = source.ContractId;
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
            target.Pending = source.Pending;
            target.InvoiceStatus = source.InvoiceStatus;
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
