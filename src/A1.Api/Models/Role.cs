using A1.Api.Models;

namespace A1.Api.Models
{
    public class Role : BaseEntity
    {
        public string RoleName { get; set; }
        public string Description { get; set; }
        public byte? Status { get; set; }
        
        
    }
}