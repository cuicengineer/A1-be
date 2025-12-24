namespace A1.Api.Models
{
    public abstract class BaseEntity
    {
        public int Id { get; set; }
        public DateTime? ActionDate { get; set; }
        public string? ActionBy { get; set; }
        public string? Action { get; set; }
        public bool? IsDeleted { get; set; }
    }
}