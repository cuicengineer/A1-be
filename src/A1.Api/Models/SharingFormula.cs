using System;

namespace A1.Api.Models
{
    public class SharingFormula : BaseEntity
    {
        public int? ClassId { get; set; }
        public int? Type { get; set; }
        public int? CmdId { get; set; }
        public int? BaseId { get; set; }
        public DateTime? ApplicableDate { get; set; }
        public DateTime? DeactiveDate { get; set; }
        public decimal AHQRate { get; set; }
        public decimal RACRate { get; set; }
        public decimal BaseRate { get; set; }
        public string? Description { get; set; }
        public string? Attachments { get; set; }
        public bool? Status { get; set; }
    }
}

