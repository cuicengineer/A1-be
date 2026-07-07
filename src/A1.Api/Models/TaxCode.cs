namespace A1.Api.Models
{
    public class TaxCode : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Status { get; set; }
    }
}
