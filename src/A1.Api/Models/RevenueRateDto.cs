using System;

namespace A1.Api.Models
{
    public class RevenueRateDto
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public int? CmdId { get; set; }
        public string? CmdName { get; set; }
        public int? BaseId { get; set; }
        public string? BaseName { get; set; }
        public int? ClassId { get; set; }
        public string? ClassName { get; set; }
        public string? PropertyIdentifier { get; set; }
        public string? UoM { get; set; }
        public decimal? Area { get; set; }
        public string? Location { get; set; }
        public string? Remarks { get; set; }
        public DateTime? ApplicableDate { get; set; }
        public decimal? Rate { get; set; }
        public string? Attachments { get; set; }
        public bool? Status { get; set; }
        public DateTime? ActionDate { get; set; }
        public string? ActionBy { get; set; }
        public string? Action { get; set; }
        public bool? IsDeleted { get; set; }
    }
}

