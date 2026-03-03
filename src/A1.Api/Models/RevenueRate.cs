using System;

namespace A1.Api.Models
{
    public class RevenueRate : BaseEntity
    {
        public int PropertyId { get; set; }
        public int? CmdId { get; set; }
        public int? BaseId { get; set; }
        public DateTime? ApplicableDate { get; set; }
        public DateTime? DeactiveDate { get; set; }
        public decimal? Rate { get; set; }
        public string? Attachments { get; set; }
        public bool? Status { get; set; }
    }
}