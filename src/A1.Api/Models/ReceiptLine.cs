using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class ReceiptLine : BaseEntity
    {
        public int ReceiptId { get; set; }

        public int LineNo { get; set; }

        [MaxLength(50)]
        public string? RacId { get; set; }

        [MaxLength(50)]
        public string? BaseId { get; set; }

        [MaxLength(300)]
        public string? Item { get; set; }

        [MaxLength(300)]
        public string? Account { get; set; }

        [MaxLength(50)]
        public string? AccountCoaId { get; set; }

        [MaxLength(100)]
        public string? PartyKey { get; set; }

        [MaxLength(50)]
        public string? PartyType { get; set; }

        [MaxLength(50)]
        public string? PartyId { get; set; }

        [MaxLength(100)]
        public string? PartyCode { get; set; }

        [MaxLength(300)]
        public string? PartyName { get; set; }

        [MaxLength(300)]
        public string? PartyLabel { get; set; }

        [MaxLength(50)]
        public string? ContractId { get; set; }

        [MaxLength(100)]
        public string? InvoiceKey { get; set; }

        [MaxLength(100)]
        public string? ContractNo { get; set; }

        [MaxLength(100)]
        public string? InvoiceNo { get; set; }

        [MaxLength(50)]
        public string? CollectionEntryId { get; set; }

        [MaxLength(100)]
        public string? TinTrn { get; set; }

        [MaxLength(100)]
        public string? TinFtn { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? Quantity { get; set; }

        [MaxLength(100)]
        public string? ProductKey { get; set; }

        [MaxLength(50)]
        public string? ProductType { get; set; }

        [MaxLength(50)]
        public string? ProductId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Tax { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        [ForeignKey(nameof(ReceiptId))]
        public Receipt? Receipt { get; set; }
    }
}
