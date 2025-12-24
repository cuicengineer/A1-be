using A1.Api.Models;

namespace A1.Api.Models
{
    public class User : BaseEntity
    {
        public string Username { get; set; }
        public string? PakNo { get; set; }
        public string? Name { get; set; }
        public string Password { get; set; }
        public string? Rank { get; set; }
        public string? Category { get; set; }
        public int? UnitId { get; set; }
        public int? BaseId { get; set; }
        public int? CmdId { get; set; }
        public byte? Status { get; set; }
    }
}