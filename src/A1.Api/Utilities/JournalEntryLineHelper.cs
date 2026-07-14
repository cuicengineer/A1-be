using System.Globalization;
using System.Text.Json;
using A1.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Utilities
{
    public static class JournalEntryLineHelper
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static List<JournalEntryLine> ResolveIncomingLines(JournalEntry item)
        {
            if (item.Lines != null && item.Lines.Count > 0)
            {
                return item.Lines;
            }

            if (string.IsNullOrWhiteSpace(item.LinesJson))
            {
                return new List<JournalEntryLine>();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<JournalEntryLineJsonDto>>(item.LinesJson, JsonOptions)
                               ?? new List<JournalEntryLineJsonDto>();
                return parsed
                    .Select((dto, index) => MapFromJsonDto(dto, index + 1))
                    .Where(line => line != null)
                    .Cast<JournalEntryLine>()
                    .ToList();
            }
            catch
            {
                return new List<JournalEntryLine>();
            }
        }

        public static async Task AttachLinesAsync(
            ApplicationDbContext context,
            IEnumerable<JournalEntry> entries,
            CancellationToken cancellationToken = default)
        {
            var entryList = entries.ToList();
            if (entryList.Count == 0)
            {
                return;
            }

            var entryIds = entryList.Select(x => x.Id).ToList();
            var lineRows = await context.JournalEntryLines
                .AsNoTracking()
                .Where(x =>
                    entryIds.Contains(x.JournalEntryId) &&
                    (x.IsDeleted == null || x.IsDeleted == false))
                .OrderBy(x => x.JournalEntryId)
                .ThenBy(x => x.LineNo)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            var linesByEntryId = lineRows
                .GroupBy(x => x.JournalEntryId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var entry in entryList)
            {
                if (linesByEntryId.TryGetValue(entry.Id, out var lines))
                {
                    entry.Lines = lines;
                    continue;
                }

                entry.Lines = TryParseLegacyLines(entry.LinesJson);
            }
        }

        public static async Task ReplaceLinesAsync(
            ApplicationDbContext context,
            JournalEntry entry,
            IReadOnlyList<JournalEntryLine> incomingLines,
            string? actionBy,
            CancellationToken cancellationToken = default)
        {
            var existingLines = await context.JournalEntryLines
                .Where(x =>
                    x.JournalEntryId == entry.Id &&
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
                var line = new JournalEntryLine
                {
                    JournalEntryId = entry.Id,
                    LineNo = index + 1,
                    AccountSource = TrimToNull(source.AccountSource, 20),
                    AccountCoaId = TrimToNull(source.AccountCoaId, 50),
                    AccountLabel = TrimToNull(source.AccountLabel, 250),
                    ContractId = TrimToNull(source.ContractId, 50),
                    ContractNo = TrimToNull(source.ContractNo, 100),
                    InvoiceKey = TrimToNull(source.InvoiceKey, 100),
                    InvoiceNo = TrimToNull(source.InvoiceNo, 100),
                    InvoiceLabel = TrimToNull(source.InvoiceLabel, 250),
                    Quantity = source.Quantity,
                    UnitPrice = source.UnitPrice,
                    Debit = Math.Round(source.Debit, 2),
                    Credit = Math.Round(source.Credit, 2),
                    IsDeleted = false,
                    Action = entry.Action,
                    ActionBy = actionBy,
                    ActionDate = now
                };
                await context.JournalEntryLines.AddAsync(line, cancellationToken);
            }

            entry.LinesJson = null;
            entry.Lines = normalizedLines
                .Select((source, index) => new JournalEntryLine
                {
                    JournalEntryId = entry.Id,
                    LineNo = index + 1,
                    AccountSource = TrimToNull(source.AccountSource, 20),
                    AccountCoaId = TrimToNull(source.AccountCoaId, 50),
                    AccountLabel = TrimToNull(source.AccountLabel, 250),
                    ContractId = TrimToNull(source.ContractId, 50),
                    ContractNo = TrimToNull(source.ContractNo, 100),
                    InvoiceKey = TrimToNull(source.InvoiceKey, 100),
                    InvoiceNo = TrimToNull(source.InvoiceNo, 100),
                    InvoiceLabel = TrimToNull(source.InvoiceLabel, 250),
                    Quantity = source.Quantity,
                    UnitPrice = source.UnitPrice,
                    Debit = Math.Round(source.Debit, 2),
                    Credit = Math.Round(source.Credit, 2)
                })
                .ToList();
        }

        public static async Task SoftDeleteLinesAsync(
            ApplicationDbContext context,
            int journalEntryId,
            string? actionBy,
            CancellationToken cancellationToken = default)
        {
            var existingLines = await context.JournalEntryLines
                .Where(x =>
                    x.JournalEntryId == journalEntryId &&
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

        private static List<JournalEntryLine> NormalizeLines(IReadOnlyList<JournalEntryLine> incomingLines)
        {
            return incomingLines
                .Select((line, index) =>
                {
                    var debit = Math.Round(ParseDecimal(line.Debit), 2);
                    var credit = Math.Round(ParseDecimal(line.Credit), 2);
                    var quantity = line.Quantity.HasValue ? Math.Round(line.Quantity.Value, 4) : (decimal?)null;
                    var unitPrice = line.UnitPrice.HasValue ? Math.Round(line.UnitPrice.Value, 2) : (decimal?)null;

                    return new JournalEntryLine
                    {
                        LineNo = line.LineNo > 0 ? line.LineNo : index + 1,
                        AccountSource = TrimToNull(line.AccountSource, 20),
                        AccountCoaId = TrimToNull(line.AccountCoaId, 50),
                        AccountLabel = TrimToNull(line.AccountLabel, 250),
                        ContractId = TrimToNull(line.ContractId, 50),
                        ContractNo = TrimToNull(line.ContractNo, 100),
                        InvoiceKey = TrimToNull(line.InvoiceKey, 100),
                        InvoiceNo = TrimToNull(line.InvoiceNo, 100),
                        InvoiceLabel = TrimToNull(line.InvoiceLabel, 250),
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        Debit = debit,
                        Credit = credit
                    };
                })
                .Where(line => line.Debit > 0 || line.Credit > 0)
                .ToList();
        }

        private static List<JournalEntryLine> TryParseLegacyLines(string? linesJson)
        {
            if (string.IsNullOrWhiteSpace(linesJson))
            {
                return new List<JournalEntryLine>();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<JournalEntryLineJsonDto>>(linesJson, JsonOptions)
                               ?? new List<JournalEntryLineJsonDto>();
                return parsed
                    .Select((dto, index) => MapFromJsonDto(dto, index + 1))
                    .Where(line => line != null)
                    .Cast<JournalEntryLine>()
                    .ToList();
            }
            catch
            {
                return new List<JournalEntryLine>();
            }
        }

        private static JournalEntryLine? MapFromJsonDto(JournalEntryLineJsonDto dto, int lineNo)
        {
            if (dto == null)
            {
                return null;
            }

            var debit = ParseDecimal(dto.Debit);
            var credit = ParseDecimal(dto.Credit);
            if (debit <= 0 && credit <= 0)
            {
                return null;
            }

            return new JournalEntryLine
            {
                LineNo = lineNo,
                AccountSource = TrimToNull(dto.AccountSource, 20),
                AccountCoaId = TrimToNull(dto.AccountCoaId, 50),
                AccountLabel = TrimToNull(dto.AccountLabel, 250),
                ContractId = TrimToNull(dto.ContractId, 50),
                ContractNo = TrimToNull(dto.ContractNo, 100),
                InvoiceKey = TrimToNull(dto.InvoiceKey, 100),
                InvoiceNo = TrimToNull(dto.InvoiceNo, 100),
                InvoiceLabel = TrimToNull(dto.InvoiceLabel, 250),
                Quantity = ParseNullableDecimal(dto.Quantity),
                UnitPrice = ParseNullableDecimal(dto.UnitPrice),
                Debit = Math.Round(debit, 2),
                Credit = Math.Round(credit, 2)
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

            var parsed = ParseDecimal(value);
            return parsed == 0m && string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture))
                ? null
                : parsed;
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

        private sealed class JournalEntryLineJsonDto
        {
            public string? AccountSource { get; set; }
            public string? AccountCoaId { get; set; }
            public string? AccountLabel { get; set; }
            public string? ContractId { get; set; }
            public string? ContractNo { get; set; }
            public string? InvoiceKey { get; set; }
            public string? InvoiceNo { get; set; }
            public string? InvoiceLabel { get; set; }
            public object? Quantity { get; set; }
            public object? UnitPrice { get; set; }
            public object? Debit { get; set; }
            public object? Credit { get; set; }
        }
    }
}
