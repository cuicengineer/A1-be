namespace A1.Api.Models
{
    public class UserPermission : BaseEntity
    {
        public int UserId { get; set; }
        public string MenuName { get; set; } = string.Empty;
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}
