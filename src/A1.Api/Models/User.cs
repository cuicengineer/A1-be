using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using A1.Api.Models;

namespace A1.Api.Models
{
    public class User : BaseEntity
    {
        public string Username { get; set; } = string.Empty;
        public string? PakNo { get; set; }
        public string? Name { get; set; }

        [JsonIgnore]
        public byte[]? Password { get; set; }

        [JsonIgnore]
        public byte[]? PasswordSalt { get; set; }

        public int? PasswordIterations { get; set; }

        [NotMapped]
        [JsonPropertyName("password")]
        public string? PlainPassword { get; set; }

        public int PasswordAttempts { get; set; } = 0;

        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }

        public string? Rank { get; set; }
        public string? Category { get; set; }
        public int? UnitId { get; set; }
        public int? BaseId { get; set; }
        public int? CmdId { get; set; }
        public byte? Status { get; set; }
    }
}