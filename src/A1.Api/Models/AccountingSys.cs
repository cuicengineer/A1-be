namespace A1.Api.Models
{
    public class AccountingSys : BaseEntity
    {
        public string ParticularName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? TelNo { get; set; }
    }
}
