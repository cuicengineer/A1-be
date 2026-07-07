using System.Globalization;
using A1.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Utilities
{
    public static class CashAndBankTrChildHelper
    {
        public static string FormatTrChildDatePart(DateTime date)
        {
            return date.ToString("dMMMyyyy", CultureInfo.InvariantCulture);
        }

        public static async Task<string> GenerateTrChildAcctIdAsync(
            ApplicationDbContext context,
            CashAndBank parentAccount,
            DateTime creationDate)
        {
            var parentAcctId = (parentAccount.AcctId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(parentAcctId))
            {
                parentAcctId = $"TR{parentAccount.Id}";
            }

            var datePart = FormatTrChildDatePart(creationDate.Date);
            var dateSuffix = $"-{datePart}";

            var existingCount = await context.CashAndBanks
                .AsNoTracking()
                .CountAsync(x =>
                    x.ParentCashAndBankId == parentAccount.Id &&
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    x.AcctId != null &&
                    x.AcctId.EndsWith(dateSuffix));

            var nextSerial = existingCount + 1;
            return $"{parentAcctId}-{nextSerial:D2}-{datePart}";
        }
    }
}
