using System;

namespace A1.Api.Models
{
    public class UserNote
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string? Content { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

