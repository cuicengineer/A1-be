using System;

namespace A1.Api.Models
{
    public class Customer : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public int? DealerId { get; set; }
        public string? Prefix { get; set; }
        public string? Rank { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? NtnCnic { get; set; }
        public string? GSTNo { get; set; }
        public string? TelNo { get; set; }
        public string? MobileNo { get; set; }
        public int? CoaId { get; set; }
        public int? CoaId2 { get; set; }
        public string? Representative { get; set; }
        public int? BankListsId { get; set; }
        public string? IBAN { get; set; }
        public bool Status { get; set; } = true;
    }
}
