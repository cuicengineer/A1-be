namespace A1.Api.Models
{
    public class ChartOfAccount : BaseEntity
    {
        public string? AcctId { get; set; }
        public string AcctName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string? SubGroup { get; set; }
        public string? ControlAccount { get; set; }
        public int SortOrder { get; set; }
        public string SectionType { get; set; } = "(A) Balance Sheet";
    }
}
