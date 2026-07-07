using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    [Table("ContractInvoicesEdit", Schema = "dbo")]
    public class ContractInvoicesEdit : BaseEntity
    {
        public int ContractId { get; set; }

        [MaxLength(50)]
        public string ContractNo { get; set; } = string.Empty;

        [MaxLength(50)]
        public string InvoiceNo { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? SubInvoiceNo { get; set; }

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime? DueDate { get; set; }

        public int? Months { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? CalculatedRentPM { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? TotalRent { get; set; }

        /// <summary>Line item: item with code (UI column Item with Code).</summary>
        [MaxLength(200)]
        public string? ItemwithCode { get; set; }

        /// <summary>Line item description (UI column Desc). Header-level period text may also use this on main rows.</summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>Line item accounting head (UI column Acc Head).</summary>
        [MaxLength(100)]
        public string? AccHead { get; set; }

        /// <summary>Line item discount percent (UI column Discount).</summary>
        public int? Discount { get; set; }

        /// <summary>Display/sort order within the invoice (UI column Order).</summary>
        public int? SortOrder { get; set; }

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
        public decimal? AmountPending { get; set; }

        [MaxLength(20)]
        public string? InvoiceStatus { get; set; }

        public DateTime? CreatedAt { get; set; }

        public bool? IsLocked { get; set; }

        /// <summary>When true, invoice appears finalized in sp_GetContractInvoiceSchedule (header row: SubInvoiceNo null).</summary>
        public bool? IsFinalized { get; set; }

        /// <summary>When true, skip configured lock-date validation (grid lock/unlock icon only).</summary>
        [NotMapped]
        public bool IgnoreLockDate { get; set; }
    }
}
