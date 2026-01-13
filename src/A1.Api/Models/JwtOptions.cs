namespace A1.Api.Models
{
    public class JwtOptions
    {
        public string Issuer { get; set; } = "A1.Api";
        public string Audience { get; set; } = "A1.Api";
        public string Key { get; set; } = "change-me-secret-key-should-be-strong";
        // These are populated from configuration (appsettings.json -> Jwt section)
        public int AccessTokenMinutes { get; set; }
        public int RefreshTokenDays { get; set; }
    }
}

