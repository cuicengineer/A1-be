using System.Text.Json;

namespace A1.Api.Models
{
    /// <summary>
    /// Singleton system notice shown to users after successful login.
    /// ContentHtml stores rich text (bold/italic/underline/colors).
    /// Status: true = Active (shown on login), false = Inactive (hidden).
    /// ExcludedUserIdsJson: JSON array of user IDs (max 5) who will not see the popup.
    /// </summary>
    public class Notice : BaseEntity
    {
        public string ContentHtml { get; set; } = string.Empty;

        public bool Status { get; set; } = true;

        /// <summary>JSON array of excluded user Ids, e.g. [1,5,9]. Max 5.</summary>
        public string? ExcludedUserIdsJson { get; set; }

        public static List<int> ParseExcludedUserIds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<int>();
            try
            {
                var ids = JsonSerializer.Deserialize<List<int>>(json);
                return (ids ?? new List<int>())
                    .Where(id => id > 0)
                    .Distinct()
                    .Take(5)
                    .ToList();
            }
            catch
            {
                return new List<int>();
            }
        }

        public static string SerializeExcludedUserIds(IEnumerable<int>? ids)
        {
            var list = (ids ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .Take(5)
                .ToList();
            return JsonSerializer.Serialize(list);
        }
    }
}
