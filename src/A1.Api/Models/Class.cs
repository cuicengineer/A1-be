using A1.Api.Models;

namespace A1.Api.Models
{
    public class Class : BaseEntity
    {
        public string Name { get; set; }
        public string? Desc { get; set; }
        public byte? Status { get; set; }
    }
}