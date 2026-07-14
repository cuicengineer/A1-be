using System.Globalization;
using System.Text.Json;
using A1.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Utilities
{
    public static class ReceiptLineHelper
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static List<ReceiptLine> ResolveIncomingLines(Receipt item)
        {
            if (item.Lines != null && item.Lines.Count > 0)
            {
                return item.Lines;
            }

            if (string.IsNullOrWhiteSpace(item.LinesJson))
            {
                return new List<ReceiptLine>();
            }

            return TryParseLegacyLines(item.LinesJson);
        }

        public static async Task AttachLinesAsync(
            ApplicationDbContext context,
            IEnumerable<Receipt> receipts,
            CancellationToken cancellationToken = default)
        {
            var receiptList = receipts.ToList();
            if (receiptList.Count == 0)
            {
                return;
            }

            var receiptIds = receiptList.Select(x => x.Id).ToList();
            var lineRows = await context.ReceiptLines
                .AsNoTracking()
                .Where(x =>
                    receiptIds.Contains(x.ReceiptId) &&
                    (x.IsDeleted == null || x.IsDeleted == false))
                .OrderBy(x => x.ReceiptId)
                .ThenBy(x => x.LineNo)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            var linesByReceiptId = lineRows
                .GroupBy(x => x.ReceiptId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var receipt in receiptList)
            {
                if (linesByReceiptId.TryGetValue(receipt.Id, out var lines))
                {
                    receipt.Lines = lines;
                    continue;
                }

                receipt.Lines = TryParseLegacyLines(receipt.LinesJson);
            }
        }

        public static async Task ReplaceLinesAsync(
            ApplicationDbContext context,
            Receipt receipt,
            IReadOnlyList<ReceiptLine> incomingLines,
            string? actionBy,
            CancellationToken cancellationToken = default)
        {
            var existingLines = await context.ReceiptLines
                .Where(x =>
                    x.ReceiptId == receipt.Id &&
                    (x.IsDeleted == null || x.IsDeleted == false))
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var existing in existingLines)
            {
                existing.IsDeleted = true;
                existing.Action = "DELETE";
                existing.ActionBy = actionBy;
                existing.ActionDate = now;
            }

            var normalizedLines = NormalizeLines(incomingLines);
            for (var index = 0; index < normalizedLines.Count; index++)
            {
                var source = normalizedLines[index];
                var line = CloneLine(source, receipt.Id, index + 1, receipt.Action, actionBy, now);
                await context.ReceiptLines.AddAsync(line, cancellationToken);
            }

            receipt.LinesJson = null;
            receipt.Lines = normalizedLines
                .Select((source, index) => CloneLine(source, receipt.Id, index + 1, null, null, null))
                .ToList();
        }

        public static async Task SoftDeleteLinesAsync(
            ApplicationDbContext context,
            int receiptId,
            string? actionBy,
            CancellationToken cancellationToken = default)
        {
            var existingLines = await context.ReceiptLines
                .Where(x =>
                    x.ReceiptId == receiptId &&
                    (x.IsDeleted == null || x.IsDeleted == false))
                .ToListAsync(cancellationToken);

            if (existingLines.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var line in existingLines)
            {
                line.IsDeleted = true;
                line.Action = "DELETE";
                line.ActionBy = actionBy;
                line.ActionDate = now;
            }
        }

        private static List<ReceiptLine> NormalizeLines(IReadOnlyList<ReceiptLine> incomingLines)
        {
            return incomingLines
                .Select((line, index) =>
                {
                    var amount = Math.Round(ParseDecimal(line.Amount), 2);
                    var discount = Math.Round(ParseDecimal(line.Discount), 2);
                    var tax = Math.Round(ParseDecimal(line.Tax), 2);
                    var total = Math.Round(ParseDecimal(line.Total), 2);
                    if (total <= 0 && amount > 0)
                    {
                        total = Math.Round(amount - discount + tax, 2);
                    }

                    return new ReceiptLine
                    {
                        LineNo = line.LineNo > 0 ? line.LineNo : index + 1,
                        RacId = TrimToNull(line.RacId, 50),
                        BaseId = TrimToNull(line.BaseId, 50),
                        Item = TrimToNull(line.Item, 300),
                        Account = TrimToNull(line.Account, 300),
                        AccountCoaId = TrimToNull(line.AccountCoaId, 50),
                        PartyKey = TrimToNull(line.PartyKey, 100),
                        PartyType = TrimToNull(line.PartyType, 50),
                        PartyId = TrimToNull(line.PartyId, 50),
                        PartyCode = TrimToNull(line.PartyCode, 100),
                        PartyName = TrimToNull(line.PartyName, 300),
                        PartyLabel = TrimToNull(line.PartyLabel, 300),
                        ContractId = TrimToNull(line.ContractId, 50),
                        InvoiceKey = TrimToNull(line.InvoiceKey, 100),
                        ContractNo = TrimToNull(line.ContractNo, 100),
                        InvoiceNo = TrimToNull(line.InvoiceNo, 100),
                        CollectionEntryId = TrimToNull(line.CollectionEntryId, 50),
                        TinTrn = TrimToNull(line.TinTrn, 100),
                        TinFtn = TrimToNull(line.TinFtn, 100),
                        Amount = amount,
                        UnitPrice = line.UnitPrice.HasValue ? Math.Round(line.UnitPrice.Value, 2) : null,
                        Quantity = line.Quantity.HasValue ? Math.Round(line.Quantity.Value, 4) : null,
                        ProductKey = TrimToNull(line.ProductKey, 100),
                        ProductType = TrimToNull(line.ProductType, 50),
                        ProductId = TrimToNull(line.ProductId, 50),
                        Discount = discount,
                        Tax = tax,
                        Total = total
                    };
                })
                .Where(line =>
                    line.Amount > 0 ||
                    line.Total > 0 ||
                    !string.IsNullOrWhiteSpace(line.Account) ||
                    !string.IsNullOrWhiteSpace(line.Item))
                .ToList();
        }

        private static ReceiptLine CloneLine(
            ReceiptLine source,
            int receiptId,
            int lineNo,
            string? action,
            string? actionBy,
            DateTime? actionDate)
        {
            return new ReceiptLine
            {
                ReceiptId = receiptId,
                LineNo = lineNo,
                RacId = TrimToNull(source.RacId, 50),
                BaseId = TrimToNull(source.BaseId, 50),
                Item = TrimToNull(source.Item, 300),
                Account = TrimToNull(source.Account, 300),
                AccountCoaId = TrimToNull(source.AccountCoaId, 50),
                PartyKey = TrimToNull(source.PartyKey, 100),
                PartyType = TrimToNull(source.PartyType, 50),
                PartyId = TrimToNull(source.PartyId, 50),
                PartyCode = TrimToNull(source.PartyCode, 100),
                PartyName = TrimToNull(source.PartyName, 300),
                PartyLabel = TrimToNull(source.PartyLabel, 300),
                ContractId = TrimToNull(source.ContractId, 50),
                InvoiceKey = TrimToNull(source.InvoiceKey, 100),
                ContractNo = TrimToNull(source.ContractNo, 100),
                InvoiceNo = TrimToNull(source.InvoiceNo, 100),
                CollectionEntryId = TrimToNull(source.CollectionEntryId, 50),
                TinTrn = TrimToNull(source.TinTrn, 100),
                TinFtn = TrimToNull(source.TinFtn, 100),
                Amount = Math.Round(source.Amount, 2),
                UnitPrice = source.UnitPrice.HasValue ? Math.Round(source.UnitPrice.Value, 2) : null,
                Quantity = source.Quantity.HasValue ? Math.Round(source.Quantity.Value, 4) : null,
                ProductKey = TrimToNull(source.ProductKey, 100),
                ProductType = TrimToNull(source.ProductType, 50),
                ProductId = TrimToNull(source.ProductId, 50),
                Discount = Math.Round(source.Discount, 2),
                Tax = Math.Round(source.Tax, 2),
                Total = Math.Round(source.Total, 2),
                IsDeleted = false,
                Action = action,
                ActionBy = actionBy,
                ActionDate = actionDate
            };
        }

        private static List<ReceiptLine> TryParseLegacyLines(string? linesJson)
        {
            if (string.IsNullOrWhiteSpace(linesJson))
            {
                return new List<ReceiptLine>();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<ReceiptLineJsonDto>>(linesJson, JsonOptions)
                               ?? new List<ReceiptLineJsonDto>();
                return parsed
                    .Select((dto, index) => MapFromJsonDto(dto, index + 1))
                    .Where(line => line != null)
                    .Cast<ReceiptLine>()
                    .ToList();
            }
            catch
            {
                return new List<ReceiptLine>();
            }
        }

        private static ReceiptLine? MapFromJsonDto(ReceiptLineJsonDto dto, int lineNo)
        {
            if (dto == null)
            {
                return null;
            }

            var amount = ParseDecimal(dto.Amount);
            var discount = ParseDecimal(dto.Discount);
            var tax = ParseDecimal(dto.Tax);
            var total = ParseDecimal(dto.Total);
            if (total <= 0 && amount > 0)
            {
                total = Math.Round(amount - discount + tax, 2);
            }

            var account = TrimToNull(dto.Account, 300);
            var item = TrimToNull(dto.Item, 300);
            if (amount <= 0 && total <= 0 && string.IsNullOrWhiteSpace(account) && string.IsNullOrWhiteSpace(item))
            {
                return null;
            }

            return new ReceiptLine
            {
                LineNo = lineNo,
                RacId = TrimToNull(dto.RacId, 50),
                BaseId = TrimToNull(dto.BaseId, 50),
                Item = item,
                Account = account,
                AccountCoaId = TrimToNull(dto.AccountCoaId, 50),
                PartyKey = TrimToNull(dto.PartyKey, 100),
                PartyType = TrimToNull(dto.PartyType, 50),
                PartyId = TrimToNull(dto.PartyId, 50),
                PartyCode = TrimToNull(dto.PartyCode, 100),
                PartyName = TrimToNull(dto.PartyName, 300),
                PartyLabel = TrimToNull(dto.PartyLabel, 300),
                ContractId = TrimToNull(dto.ContractId, 50),
                InvoiceKey = TrimToNull(dto.InvoiceKey, 100),
                ContractNo = TrimToNull(dto.ContractNo, 100),
                InvoiceNo = TrimToNull(dto.InvoiceNo, 100),
                CollectionEntryId = TrimToNull(dto.CollectionEntryId, 50),
                TinTrn = TrimToNull(dto.TinTrn, 100),
                TinFtn = TrimToNull(dto.TinFtn, 100),
                Amount = Math.Round(amount, 2),
                UnitPrice = ParseNullableDecimal(dto.UnitPrice),
                Quantity = ParseNullableDecimal(dto.Quantity),
                ProductKey = TrimToNull(dto.ProductKey, 100),
                ProductType = TrimToNull(dto.ProductType, 50),
                ProductId = TrimToNull(dto.ProductId, 50),
                Discount = Math.Round(discount, 2),
                Tax = Math.Round(tax, 2),
                Total = Math.Round(total, 2)
            };
        }

        private static decimal ParseDecimal(object? value)
        {
            if (value == null)
            {
                return 0m;
            }

            if (value is decimal decimalValue)
            {
                return decimalValue;
            }

            if (value is double doubleValue)
            {
                return (decimal)doubleValue;
            }

            if (value is float floatValue)
            {
                return (decimal)floatValue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return longValue;
            }

            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetDecimal(out var jsonDecimal))
                {
                    return jsonDecimal;
                }

                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    return ParseDecimal(jsonElement.GetString());
                }

                return 0m;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0m;
            }

            return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0m;
        }

        private static decimal? ParseNullableDecimal(object? value)
        {
            if (value == null)
            {
                return null;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return Math.Round(ParseDecimal(value), 4);
        }

        private static string? TrimToNull(string? value, int maxLength)
        {
            var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (text == null)
            {
                return null;
            }

            return text.Length <= maxLength ? text : text[..maxLength];
        }

        private sealed class ReceiptLineJsonDto
        {
            public string? RacId { get; set; }
            public string? BaseId { get; set; }
            public string? Item { get; set; }
            public string? Account { get; set; }
            public string? AccountCoaId { get; set; }
            public string? PartyKey { get; set; }
            public string? PartyType { get; set; }
            public string? PartyId { get; set; }
            public string? PartyCode { get; set; }
            public string? PartyName { get; set; }
            public string? PartyLabel { get; set; }
            public string? ContractId { get; set; }
            public string? InvoiceKey { get; set; }
            public string? ContractNo { get; set; }
            public string? InvoiceNo { get; set; }
            public string? CollectionEntryId { get; set; }
            public string? TinTrn { get; set; }
            public string? TinFtn { get; set; }
            public object? Amount { get; set; }
            public object? UnitPrice { get; set; }
            public object? Quantity { get; set; }
            public string? ProductKey { get; set; }
            public string? ProductType { get; set; }
            public string? ProductId { get; set; }
            public object? Discount { get; set; }
            public object? Tax { get; set; }
            public object? Total { get; set; }
        }
    }
}
