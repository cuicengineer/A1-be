using System.ComponentModel.DataAnnotations.Schema;

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

        /// <summary>Receipt or Payment — used to share the Receipts table.</summary>
        public string? RecordType { get; set; }

        /// <summary>Payment voucher number (e.g. TE-0001/2025).</summary>
        public string? VrNo { get; set; }

        /// <summary>Cash &amp; Bank account the payment is received from.</summary>
        public int? CashAndBankAccountId { get; set; }

        [NotMapped]
        public string? ReceivedFromAccountDisplay { get; set; }
    }
}
