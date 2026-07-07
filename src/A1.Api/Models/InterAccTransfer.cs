using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class InterAccTransfer : BaseEntity
    {
        [Column(TypeName = "date")]
        public DateTime TransferDate { get; set; }

        [MaxLength(50)]
        public string VrNo { get; set; } = string.Empty;

        [MaxLength(15)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? Particulars { get; set; }

        public int PaidFromAccountId { get; set; }

        [MaxLength(50)]
        public string? SettlementVrNo { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidFromAmount { get; set; }

        public int ReceivedInAccountId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ReceivedInAmount { get; set; }

        [MaxLength(50)]
        public string? TinFtn { get; set; }

        [MaxLength(20)]
        public string? Status { get; set; }

        [NotMapped]
        public string? PaidFromAccountDisplay { get; set; }

        [NotMapped]
        public string? ReceivedInAccountDisplay { get; set; }

        [NotMapped]
        public string? PaidFromCurrency { get; set; }

        [NotMapped]
        public string? ReceivedInCurrency { get; set; }
    }
}
