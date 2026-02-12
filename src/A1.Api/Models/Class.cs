using A1.Api.Models;

namespace A1.Api.Models
{
    public class Class : BaseEntity
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string? Description { get; set; }
        public byte? Status { get; set; }
    }
}