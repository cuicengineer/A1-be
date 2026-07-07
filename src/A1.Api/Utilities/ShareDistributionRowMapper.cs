using System.Data;
using A1.Api.Models;

namespace A1.Api.Utilities
{
    public static class ShareDistributionRowMapper
    {
        public static ShareDistributionRow Map(IDataRecord record)
        {
            return new ShareDistributionRow
            {
                SN = GetInt(record, "SN") ?? 0,
                Id = GetInt(record, "Id") ?? 0,
                ContractNo = GetString(record, "ContractNo"),
                RACName = GetString(record, "RACName"),
                Base = GetString(record, "Base"),
                Class = GetString(record, "Class"),
                Agreement = GetString(record, "Agreement"),
                TenantAndBusiness = GetString(record, "TenantAndBusiness"),
                BoOArea = GetDecimal(record, "BoOArea"),
                RRFY = GetString(record, "RRFY"),
                RevenueRate = GetDecimal(record, "RevenueRate"),
                GovtSharePA = GetDecimal(record, "GovtSharePA"),
                CurrentRentPA = GetDecimal(record, "CurrentRentPA"),
                ReceiptDate = GetDateTime(record, "ReceiptDate"),
                ReceiptAmount = GetDecimal(record, "ReceiptAmount"),
                Ratio = GetDecimal(record, "Ratio"),
                Govt = GetDecimal(record, "Govt"),
                PAF = GetDecimal(record, "PAF"),
                AHQ = GetDecimal(record, "AHQ"),
                RAC = GetDecimal(record, "RAC"),
                BaseShare = GetDecimal(record, "BaseShare"),
                Workbook = GetString(record, "Workbook"),
                CAId = GetString(record, "CAId"),
                CAArea1 = GetDecimal(record, "CAArea1"),
                CAArea2 = GetDecimal(record, "CAArea2"),
            };
        }

        private static int? GetInt(IDataRecord record, string name)
        {
            var value = GetValue(record, name);
            if (value == null) return null;
            return Convert.ToInt32(value);
        }

        private static decimal? GetDecimal(IDataRecord record, string name)
        {
            var value = GetValue(record, name);
            if (value == null) return null;
            return Convert.ToDecimal(value);
        }

        private static DateTime? GetDateTime(IDataRecord record, string name)
        {
            var value = GetValue(record, name);
            if (value == null) return null;
            return Convert.ToDateTime(value);
        }

        private static string? GetString(IDataRecord record, string name)
        {
            var value = GetValue(record, name);
            return value == null ? null : Convert.ToString(value);
        }

        private static object? GetValue(IDataRecord record, string name)
        {
            for (var i = 0; i < record.FieldCount; i++)
            {
                if (!string.Equals(record.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return record.IsDBNull(i) ? null : record.GetValue(i);
            }

            return null;
        }
    }
}
