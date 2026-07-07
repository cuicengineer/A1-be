namespace A1.Api.Models
{
    public class ProductUom : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Status { get; set; }
    }
}
