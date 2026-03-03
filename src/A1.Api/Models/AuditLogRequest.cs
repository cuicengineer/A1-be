namespace A1.Api.Models
{
    /// <summary>
    /// Request DTO for logging an audit entry. ActionBy is set from the authenticated user.
    /// </summary>
    public class AuditLogRequest
    {
        public string EntityName { get; set; } = string.Empty;
        public long? EntityId { get; set; }
        public string? OldValuesJson { get; set; }
        public string? NewValuesJson { get; set; }
        /// <summary>Source of the action: API, Web, or Job.</summary>
        public string Action { get; set; } = "API";
    }
}
