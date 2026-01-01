using System;

namespace A1.Api.Models
{
    public class Tenant : BaseEntity
    {
        public string TenantNo { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? Prefix { get; set; }
        public string? BusinessName { get; set; }
        public string? Address { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? TelephoneNo { get; set; }
        public string? CellNo { get; set; }
        public string? NTNNo { get; set; }
        public string? GSTNo { get; set; }
        public bool Status { get; set; }
        public string? Remarks { get; set; }
    }
}

