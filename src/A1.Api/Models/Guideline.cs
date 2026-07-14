using System.ComponentModel.DataAnnotations;

namespace A1.Api.Models
{
    public class Guideline : BaseEntity
    {
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool Status { get; set; } = true;
    }
}
