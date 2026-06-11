namespace A1.Api.Models
{
    public class ChartOfAccountsReorderRequest
    {
        public int Id { get; set; }
        public string Direction { get; set; } = string.Empty;
        public string? SectionType { get; set; }
    }
}
