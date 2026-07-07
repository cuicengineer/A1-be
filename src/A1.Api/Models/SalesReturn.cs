using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class SalesReturn : BaseEntity
    {
        [Column(TypeName = "date")]
        public DateTime? Date { get; set; }

        public string? VrNo { get; set; }

        public string? ContractCustomerKey { get; set; }

        public string? ContractCustomerLabel { get; set; }

        public int? ContractId { get; set; }

        public string? ContractNo { get; set; }

        public int? CustomerId { get; set; }

        public string? InvoiceKey { get; set; }

        public string? InvoiceNo { get; set; }

        public string? InvoiceLabel { get; set; }

        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? GrandTotal { get; set; }

        public string? LinesJson { get; set; }

        public string? AttachmentsJson { get; set; }
    }
}
