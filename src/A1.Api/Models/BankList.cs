namespace A1.Api.Models
{
    public class BankList : BaseEntity
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public string? Address { get; set; }
        public byte? Status { get; set; }
    }
}

