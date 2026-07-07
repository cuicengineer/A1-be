using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class PurchaseReturn : BaseEntity
    {
        [Column(TypeName = "date")]
        public DateTime? Date { get; set; }

        public string? VrNo { get; set; }

        public string? SupplierKey { get; set; }

        public string? SupplierLabel { get; set; }

        public int? SupplierId { get; set; }

        public string? SupplierCode { get; set; }

        public string? PurchaseInvoiceNo { get; set; }

        public string? PurchaseInvoiceLabel { get; set; }

        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? GrandTotal { get; set; }

        public string? LinesJson { get; set; }
    }
}
