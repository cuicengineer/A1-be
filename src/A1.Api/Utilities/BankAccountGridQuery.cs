using A1.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Utilities
{
    public static class BankAccountGridQuery
    {
        public static async Task<(List<BankAccount> Rows, int TotalCount)> QueryAsync(
            ApplicationDbContext context,
            DataAccessScope scope,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await QueryViaStoredProcedureAsync(context, scope, pageNumber, pageSize, cancellationToken);
            }
            catch (Exception ex) when (ShouldFallbackToEntityFramework(ex))
            {
                return await QueryViaEntityFrameworkAsync(context, scope, pageNumber, pageSize, cancellationToken);
            }
        }

        private static bool ShouldFallbackToEntityFramework(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is SqlException sql)
                {
                    // 2812 = could not find stored procedure
                    if (sql.Number == 2812)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static async Task<(List<BankAccount> Rows, int TotalCount)> QueryViaStoredProcedureAsync(
            ApplicationDbContext context,
            DataAccessScope scope,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var accessLevel = scope?.AccessLevel ?? "ahq";
            var allowedBaseCsv = scope?.AllowedBaseIds != null && scope.AllowedBaseIds.Count > 0
                ? string.Join(",", scope.AllowedBaseIds)
                : null;

            var (rows, totalCount) = await StoredProcedureReader.ExecuteWithOutputCountAsync(
                context,
                "dbo.sp_get_instbankacc",
                command =>
                {
                    StoredProcedureReader.AddIntParameter(command, "@PageNumber", pageNumber);
                    StoredProcedureReader.AddIntParameter(command, "@PageSize", pageSize);
                    StoredProcedureReader.AddStringParameter(command, "@AccessLevel", accessLevel);
                    StoredProcedureReader.AddIntParameter(command, "@ScopeCmdId", scope?.CmdId);
                    StoredProcedureReader.AddIntParameter(command, "@ScopeBaseId", scope?.BaseId);
                    StoredProcedureReader.AddStringParameter(command, "@AllowedBaseIdsCsv", allowedBaseCsv);
                    StoredProcedureReader.AddOutputIntParameter(command, "@TotalCount");
                },
                cancellationToken: cancellationToken);

            return (rows.Select(MapRow).ToList(), totalCount);
        }

        private static async Task<(List<BankAccount> Rows, int TotalCount)> QueryViaEntityFrameworkAsync(
            ApplicationDbContext context,
            DataAccessScope scope,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var baseQuery = context.BankAccounts
                .AsNoTracking()
                .Where(a => a.IsDeleted == null || a.IsDeleted == false);
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            var totalCount = await baseQuery.CountAsync(cancellationToken);

            var pageRowsQuery =
                from acct in baseQuery
                join rac in context.AccRacBases.AsNoTracking()
                    on acct.CmdId equals rac.Id into racGroup
                from rac in racGroup.DefaultIfEmpty()
                join unit in context.AccRacBases.AsNoTracking()
                    on acct.BaseId equals unit.Id into unitGroup
                from unit in unitGroup.DefaultIfEmpty()
                orderby acct.Id descending
                select new
                {
                    acct,
                    CmdName = rac != null ? rac.Name ?? string.Empty : string.Empty,
                    BaseName = unit != null ? unit.Name ?? string.Empty : string.Empty
                };

            var pageRows = await PaginationHelper
                .ApplyPaging(pageRowsQuery, pageNumber, pageSize)
                .ToListAsync(cancellationToken);

            var payload = new List<BankAccount>(pageRows.Count);
            foreach (var row in pageRows)
            {
                row.acct.CmdName = row.CmdName;
                row.acct.BaseName = row.BaseName;
                payload.Add(row.acct);
            }

            return (payload, totalCount);
        }

        private static BankAccount MapRow(Dictionary<string, object?> row)
        {
            return new BankAccount
            {
                Id = StoredProcedureReader.ReadInt(row, "Id") ?? 0,
                OpeningDate = StoredProcedureReader.ReadDateTime(row, "OpeningDate") ?? default,
                CmdId = StoredProcedureReader.ReadInt(row, "CmdId", "RAC"),
                BaseId = StoredProcedureReader.ReadInt(row, "BaseId", "Base"),
                FundingSource = StoredProcedureReader.ReadString(row, "FundingSource"),
                FundName = StoredProcedureReader.ReadString(row, "FundName"),
                TitleOfAccount = StoredProcedureReader.ReadString(row, "TitleOfAccount"),
                BankName = StoredProcedureReader.ReadString(row, "BankName"),
                BranchCode = StoredProcedureReader.ReadString(row, "BranchCode"),
                BranchAddress = StoredProcedureReader.ReadString(row, "BranchAddress"),
                IBAN = StoredProcedureReader.ReadString(row, "IBAN") ?? string.Empty,
                Currency = StoredProcedureReader.ReadString(row, "Currency"),
                AccountType = StoredProcedureReader.ReadString(row, "AccountType"),
                SignatoryDate = StoredProcedureReader.ReadDateTime(row, "SignatoryDate"),
                Signatory1 = StoredProcedureReader.ReadString(row, "Signatory1") ?? string.Empty,
                Signatory2 = StoredProcedureReader.ReadString(row, "Signatory2") ?? string.Empty,
                Signatory3 = StoredProcedureReader.ReadString(row, "Signatory3"),
                StatusDate = StoredProcedureReader.ReadDateTime(row, "StatusDate"),
                Remarks = StoredProcedureReader.ReadString(row, "Remarks"),
                Authority = StoredProcedureReader.ReadString(row, "Authority"),
                Reference = StoredProcedureReader.ReadString(row, "Reference"),
                CreatedDate = StoredProcedureReader.ReadDateTime(row, "CreatedDate"),
                AccStatus = StoredProcedureReader.ReadString(row, "AccStatus"),
                ActionDate = StoredProcedureReader.ReadDateTime(row, "ActionDate"),
                ActionBy = StoredProcedureReader.ReadString(row, "ActionBy"),
                Action = StoredProcedureReader.ReadString(row, "Action"),
                IsDeleted = StoredProcedureReader.ReadNullableBool(row, "IsDeleted"),
                CmdName = StoredProcedureReader.ReadString(row, "CmdName"),
                BaseName = StoredProcedureReader.ReadString(row, "BaseName"),
            };
        }
    }
}
