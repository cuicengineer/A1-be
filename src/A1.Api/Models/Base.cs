using A1.Api.Models;

namespace A1.Api.Models
{
    public class Base : BaseEntity
    {
        public string Name { get; set; }
        public int Cmd { get; set; }
        public int Status { get; set; }
    }
}