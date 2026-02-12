using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class ContractRiseTerm : BaseEntity
    {
        [Key]
        [Column("RiseTermID")]
        public new int Id { get; set; }

        [Column("ContractID")]
        public int ContractId { get; set; }
        public int MonthsInterval { get; set; }
        public decimal RisePercent { get; set; }
        public int SequenceNo { get; set; }
        public byte? Status { get; set; }
    }
}