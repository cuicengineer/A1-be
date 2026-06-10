namespace A1.Api.Models
{
    public class UserAppoint : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public byte? Status { get; set; }
    }
}
