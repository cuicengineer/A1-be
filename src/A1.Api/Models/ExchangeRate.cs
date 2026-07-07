using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class ExchangeRate : BaseEntity
    {
        [Column(TypeName = "date")]
        public DateTime RateDate { get; set; }

        public int BaseCurrencyId { get; set; }

        public int ForeignCurrencyId { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal Rate { get; set; }

        [NotMapped]
        public string? BaseCurrencyDisplay { get; set; }

        [NotMapped]
        public string? ForeignCurrencyDisplay { get; set; }

        [NotMapped]
        public string? BaseCurrencyCode { get; set; }
    }
}
