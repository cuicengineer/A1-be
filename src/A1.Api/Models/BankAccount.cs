using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class BankAccount : BaseEntity
    {
        [Column(TypeName = "date")]
        public DateTime OpeningDate { get; set; }

        [Column("RAC")]
        public int? CmdId { get; set; }

        [Column("Base")]
        public int? BaseId { get; set; }

        [MaxLength(50)]
        public string? FundingSource { get; set; }

        [MaxLength(100)]
        public string? FundName { get; set; }

        [MaxLength(150)]
        public string? TitleOfAccount { get; set; }

        [MaxLength(150)]
        public string? BankName { get; set; }

        [MaxLength(20)]
        public string? BranchCode { get; set; }

        [MaxLength(200)]
        public string? BranchAddress { get; set; }

        [MaxLength(34)]
        public string IBAN { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Currency { get; set; }

        [MaxLength(50)]
        public string? AccountType { get; set; }

        [Column(TypeName = "date")]
        public DateTime? SignatoryDate { get; set; }

        [MaxLength(100)]
        public string Signatory1 { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Signatory2 { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Signatory3 { get; set; }

        [Column(TypeName = "date")]
        public DateTime? StatusDate { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }

        [MaxLength(50)]
        public string? Authority { get; set; }

        [MaxLength(150)]
        public string? Reference { get; set; }

        public DateTime? CreatedDate { get; set; }

        [NotMapped]
        public string? CmdName { get; set; }

        [NotMapped]
        public string? BaseName { get; set; }
    }
}
