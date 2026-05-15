using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    [Table("ContractInvoicesEdit", Schema = "dbo")]
    public class ContractInvoicesEdit
    {
        [Key]
        public int Id { get; set; }

        public int ContractId { get; set; }

        [MaxLength(50)]
        public string ContractNo { get; set; } = string.Empty;

        [MaxLength(50)]
        public string InvoiceNo { get; set; } = string.Empty;

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime? DueDate { get; set; }

        public int? Months { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? CalculatedRentPM { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? TotalRent { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }

        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }

        [MaxLength(100)]
        public string? ContractPeriod { get; set; }

        [MaxLength(200)]
        public string? BusinessName { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? InitialRentPM { get; set; }

        public int? PaymentTermMonths { get; set; }

        [MaxLength(50)]
        public string? RiseTermType { get; set; }

        [MaxLength(50)]
        public string? RiseTerm { get; set; }

        [MaxLength(20)]
        public string? RiseRate { get; set; }

        public DateTime? RiseDate { get; set; }

        [MaxLength(50)]
        public string? InvoiceDateType { get; set; }

        public int? CmdId { get; set; }
        public int? BaseId { get; set; }
        public int? ClassId { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? AmountReceived { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? AmountReceivable { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? Pending { get; set; }

        [MaxLength(20)]
        public string? InvoiceStatus { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
