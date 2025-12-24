using A1.Api.Models;

namespace A1.Api.Models
{
    public class Unit : BaseEntity
    {
        public string Name { get; set; }
        public int Cmd { get; set; }
        public int Base { get; set; }
        public int Status { get; set; }
    }
}