using System;

namespace A1.Api.Models
{
    public class RevenueRate : BaseEntity
    {
        public int PropertyId { get; set; }
        public DateTime? ApplicableDate { get; set; }
        public decimal? Rate { get; set; }
        public string? Attachments { get; set; }
        public bool? Status { get; set; }
    }
}