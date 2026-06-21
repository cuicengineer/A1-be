namespace A1.Api.Models
{
    public class SupplierCodePrefix : BaseEntity
    {
        public string PrefixAlpha { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
