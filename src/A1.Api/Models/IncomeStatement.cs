namespace A1.Api.Models
{
    public class IncomeStatement : BaseEntity
    {
        public string? AcctId { get; set; }
        public string AcctName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string? SubGroup { get; set; }
        public int SortOrder { get; set; }
    }
}
