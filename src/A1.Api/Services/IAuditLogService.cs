using A1.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace A1.Api.Services
{
    /// <summary>
    /// Writes audit log entries using a new scope per call to avoid shared context and reduce contention.
    /// </summary>
    public interface IAuditLogService
    {
        Task LogAsync(AuditLog entry, System.Threading.CancellationToken cancellationToken = default);
        Task LogBatchAsync(IEnumerable<AuditLog> entries, System.Threading.CancellationToken cancellationToken = default);
    }
}
