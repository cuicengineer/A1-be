using A1.Api.Models;

namespace A1.Api.Models
{
    public class Command : BaseEntity
    {
        public string Name { get; set; }
        public string Abb { get; set; }
        public int Status { get; set; }
    }
}