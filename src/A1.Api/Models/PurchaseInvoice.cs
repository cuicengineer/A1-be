using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class PurchaseInvoice : BaseEntity
    {
        [Column(TypeName = "date")]
        public DateTime? Date { get; set; }

        public string? PiNo { get; set; }

        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? GrandTotal { get; set; }

        public string? LinesJson { get; set; }
    }
}
