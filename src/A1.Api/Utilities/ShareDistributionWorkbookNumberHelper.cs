using System.Globalization;

namespace A1.Api.Utilities
{
    public static class ShareDistributionWorkbookNumberHelper
    {
        private static readonly string[] MonthTokens =
        {
            "JAN", "FEB", "MAR", "APR", "MAY", "JUN",
            "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"
        };

        public static string FormatDateSuffix(DateTime date)
        {
            var month = MonthTokens[date.Month - 1];
            var year = (date.Year % 100).ToString("00", CultureInfo.InvariantCulture);
            return $"{date.Day:00}{month}{year}";
        }

        public static string FormatWorkbookNumber(int serial, DateTime createdDate)
        {
            return $"WB{serial:00}-{FormatDateSuffix(createdDate)}";
        }
    }
}
