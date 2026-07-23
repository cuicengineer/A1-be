using A1.Api.Models;

namespace A1.Api.Models
{
    public class Class : BaseEntity
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public string? Description { get; set; }
        public string? UoM { get; set; }

        /// <summary>Linked Product/Service Item with Code used when finalizing contract invoices.</summary>
        public string? ItemWithCode { get; set; }

        public byte? Status { get; set; }
    }
}