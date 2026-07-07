using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class Currency : BaseEntity
    {
        [MaxLength(10)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("Type")]
        [MaxLength(20)]
        public string CurrencyType { get; set; } = "Base";

        public int DecimalPlaces { get; set; }

        public byte? Status { get; set; }
    }
}
