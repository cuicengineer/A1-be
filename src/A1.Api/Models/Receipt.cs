namespace A1.Api.Models
{
    public class Receipt : BaseEntity
    {
        public DateTime? Date { get; set; }
        public string? Month { get; set; }
        public bool? ReferenceAutomatic { get; set; }
        public string? Reference { get; set; }
        public string? PaidFrom { get; set; }
        public string? PayeeContactType { get; set; }
        public int? PayeePartyId { get; set; }
        public string? PayeePartyCode { get; set; }
        public string? PayeeName { get; set; }
        public string? Description { get; set; }
        public decimal? GrandTotal { get; set; }
        public string? LinesJson { get; set; }
        public string? AttachmentsJson { get; set; }
        public bool? FinalizedByAhq { get; set; }
    }
}
