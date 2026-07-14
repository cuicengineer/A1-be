using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class JournalEntryLine : BaseEntity
    {
        public int JournalEntryId { get; set; }

        public int LineNo { get; set; }

        [MaxLength(20)]
        public string? AccountSource { get; set; }

        [MaxLength(50)]
        public string? AccountCoaId { get; set; }

        [MaxLength(250)]
        public string? AccountLabel { get; set; }

        [MaxLength(50)]
        public string? ContractId { get; set; }

        [MaxLength(100)]
        public string? ContractNo { get; set; }

        [MaxLength(100)]
        public string? InvoiceKey { get; set; }

        [MaxLength(100)]
        public string? InvoiceNo { get; set; }

        [MaxLength(250)]
        public string? InvoiceLabel { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Debit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Credit { get; set; }

        [ForeignKey(nameof(JournalEntryId))]
        public JournalEntry? JournalEntry { get; set; }
    }
}
