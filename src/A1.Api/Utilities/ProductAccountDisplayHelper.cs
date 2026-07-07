using A1.Api.Models;
using A1.Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    internal static class ProductAccountDisplayHelper
    {
        public static string FormatCoaLabel(ChartOfAccount? row)
        {
            if (row == null) return string.Empty;
            var acctId = (row.AcctId ?? string.Empty).Trim();
            var acctName = (row.AcctName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(acctId) && !string.IsNullOrEmpty(acctName))
            {
                return $"{acctId}-{acctName}";
            }
            return !string.IsNullOrEmpty(acctName) ? acctName : acctId;
        }

        public static string FormatIncomeStatementLabel(IncomeStatement? row)
        {
            if (row == null) return string.Empty;
            var acctId = (row.AcctId ?? string.Empty).Trim();
            var acctName = (row.AcctName ?? string.Empty).Trim();
            var group = (row.GroupName ?? string.Empty).Trim();
            var baseLabel = !string.IsNullOrEmpty(acctId) && !string.IsNullOrEmpty(acctName)
                ? $"{acctId}-{acctName}"
                : !string.IsNullOrEmpty(acctName) ? acctName : acctId;
            return !string.IsNullOrEmpty(group) ? $"{baseLabel} ({group})" : baseLabel;
        }

        public static async Task<string> ResolveAccountDisplayAsync(
            ApplicationDbContext context,
            int? coaId,
            int? incomeStatementId)
        {
            if (incomeStatementId.HasValue && incomeStatementId.Value > 0)
            {
                var row = await context.IncomeStatements
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == incomeStatementId.Value &&
                        (x.IsDeleted == null || x.IsDeleted == false));
                return FormatIncomeStatementLabel(row);
            }

            if (coaId.HasValue && coaId.Value > 0)
            {
                var row = await context.ChartOfAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == coaId.Value &&
                        (x.IsDeleted == null || x.IsDeleted == false));
                return FormatCoaLabel(row);
            }

            return string.Empty;
        }

        public static async Task<string> ResolveTaxCodeDisplayAsync(ApplicationDbContext context, int? taxCodeId)
        {
            if (!taxCodeId.HasValue || taxCodeId.Value <= 0) return string.Empty;
            var row = await context.TaxCodes
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == taxCodeId.Value &&
                    (x.IsDeleted == null || x.IsDeleted == false));
            return row == null
                ? string.Empty
                : string.IsNullOrWhiteSpace(row.Description)
                    ? (row.Code ?? string.Empty).Trim()
                    : $"{(row.Code ?? string.Empty).Trim()} {row.Description.Trim()}".Trim();
        }
    }
}
