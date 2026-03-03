using System;

namespace A1.Api.Models
{
    /// <summary>
    /// Audit log entry for edit actions. Does not inherit BaseEntity (no IsDeleted, different PK type).
    /// </summary>
    public class AuditLog
    {
        public long Id { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public long? EntityId { get; set; }
        public string? OldValuesJson { get; set; }
        public string? NewValuesJson { get; set; }
        public string ActionBy { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // API / Web / Job
        public DateTime ActionDateTime { get; set; }
    }
}
