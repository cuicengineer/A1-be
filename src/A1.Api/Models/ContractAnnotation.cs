using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace A1.Api.Models
{
    public class ContractAnnotation : BaseEntity
    {
        public int ContractId { get; set; }

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        [MaxLength(150)]
        [JsonPropertyName("remarksBy")]
        public string? RemarksBy { get; set; }
    }
}
