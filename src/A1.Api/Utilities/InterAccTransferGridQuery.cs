using A1.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Utilities
{
    public static class InterAccTransferGridQuery
    {
        public static async Task<(List<InterAccTransfer> Rows, int TotalCount)> QueryAsync(
            ApplicationDbContext context,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var (rows, totalCount) = await StoredProcedureReader.ExecuteWithOutputCountAsync(
                context,
                "dbo.sp_GetInterAccTransfers",
                command =>
                {
                    StoredProcedureReader.AddIntParameter(command, "@PageNumber", pageNumber);
                    StoredProcedureReader.AddIntParameter(command, "@PageSize", pageSize);
                    StoredProcedureReader.AddOutputIntParameter(command, "@TotalCount");
                },
                cancellationToken: cancellationToken);

            return (rows.Select(MapRow).ToList(), totalCount);
        }

        private static InterAccTransfer MapRow(Dictionary<string, object?> row)
        {
            return new InterAccTransfer
            {
                Id = StoredProcedureReader.ReadInt(row, "Id") ?? 0,
                TransferDate = StoredProcedureReader.ReadDateTime(row, "TransferDate") ?? default,
                VrNo = StoredProcedureReader.ReadString(row, "VrNo") ?? string.Empty,
                Description = StoredProcedureReader.ReadString(row, "Description"),
                Particulars = StoredProcedureReader.ReadString(row, "Particulars"),
                PaidFromAccountId = StoredProcedureReader.ReadInt(row, "PaidFromAccountId") ?? 0,
                SettlementVrNo = StoredProcedureReader.ReadString(row, "SettlementVrNo"),
                PaidFromAmount = StoredProcedureReader.ReadDecimal(row, "PaidFromAmount"),
                ReceivedInAccountId = StoredProcedureReader.ReadInt(row, "ReceivedInAccountId") ?? 0,
                ReceivedInAmount = StoredProcedureReader.ReadDecimal(row, "ReceivedInAmount"),
                TinFtn = StoredProcedureReader.ReadString(row, "TinFtn"),
                Status = StoredProcedureReader.ReadString(row, "Status"),
                ActionDate = StoredProcedureReader.ReadDateTime(row, "ActionDate"),
                ActionBy = StoredProcedureReader.ReadString(row, "ActionBy"),
                Action = StoredProcedureReader.ReadString(row, "Action"),
                IsDeleted = StoredProcedureReader.ReadNullableBool(row, "IsDeleted"),
                PaidFromAccountDisplay = StoredProcedureReader.ReadString(row, "PaidFromAccountDisplay"),
                ReceivedInAccountDisplay = StoredProcedureReader.ReadString(row, "ReceivedInAccountDisplay"),
                PaidFromCurrency = StoredProcedureReader.ReadString(row, "PaidFromCurrency"),
                ReceivedInCurrency = StoredProcedureReader.ReadString(row, "ReceivedInCurrency"),
            };
        }
    }
}
