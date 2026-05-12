using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class AccRacBase : BaseEntity
    {
        [Column("NAME")]
        public string? Name { get; set; }

        [Column("Type")]
        public string Type { get; set; } = string.Empty;

        public int? ParentId { get; set; }

        public byte? Status { get; set; }
    }
}
