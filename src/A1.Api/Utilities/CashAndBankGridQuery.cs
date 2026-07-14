using A1.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Utilities
{
    public static class CashAndBankGridQuery
    {
        public static async Task<(List<CashAndBank> Rows, int TotalCount)> QueryAsync(
            ApplicationDbContext context,
            int pageNumber,
            int pageSize,
            int? parentCashAndBankId,
            bool topLevelOnly,
            CancellationToken cancellationToken = default)
        {
            var (rows, totalCount) = await StoredProcedureReader.ExecuteWithOutputCountAsync(
                context,
                "dbo.sp_GetCashAndBanks",
                command =>
                {
                    StoredProcedureReader.AddIntParameter(command, "@PageNumber", pageNumber);
                    StoredProcedureReader.AddIntParameter(command, "@PageSize", pageSize);
                    StoredProcedureReader.AddIntParameter(command, "@ParentCashAndBankId", parentCashAndBankId);
                    StoredProcedureReader.AddBoolParameter(command, "@TopLevelOnly", topLevelOnly);
                    StoredProcedureReader.AddOutputIntParameter(command, "@TotalCount");
                },
                cancellationToken: cancellationToken);

            return (rows.Select(MapRow).ToList(), totalCount);
        }

        private static CashAndBank MapRow(Dictionary<string, object?> row)
        {
            return new CashAndBank
            {
                Id = StoredProcedureReader.ReadInt(row, "Id") ?? 0,
                AcctId = StoredProcedureReader.ReadString(row, "AcctId"),
                Name = StoredProcedureReader.ReadString(row, "Name") ?? string.Empty,
                CoaId = StoredProcedureReader.ReadInt(row, "CoaId"),
                Currency = StoredProcedureReader.ReadString(row, "Currency") ?? string.Empty,
                Mode = StoredProcedureReader.ReadString(row, "Mode") ?? string.Empty,
                IBAN = StoredProcedureReader.ReadString(row, "IBAN"),
                BankListsId = StoredProcedureReader.ReadInt(row, "BankListsId"),
                Status = StoredProcedureReader.ReadString(row, "Status"),
                ParentCashAndBankId = StoredProcedureReader.ReadInt(row, "ParentCashAndBankId"),
                ActionDate = StoredProcedureReader.ReadDateTime(row, "ActionDate"),
                ActionBy = StoredProcedureReader.ReadString(row, "ActionBy"),
                Action = StoredProcedureReader.ReadString(row, "Action"),
                IsDeleted = StoredProcedureReader.ReadNullableBool(row, "IsDeleted"),
                CoaDisplay = StoredProcedureReader.ReadString(row, "CoaDisplay"),
                BankDisplay = StoredProcedureReader.ReadString(row, "BankDisplay"),
                ChildCount = StoredProcedureReader.ReadInt(row, "ChildCount") ?? 0,
                IsReferencedByInterAccTransfer = StoredProcedureReader.ReadBool(row, "IsReferencedByInterAccTransfer"),
            };
        }
    }
}
