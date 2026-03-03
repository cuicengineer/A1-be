using A1.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace A1.Api.Services
{
    /// <summary>
    /// Writes to AuditLog using a new DbContext scope per call to avoid deadlocks and avoid holding request context.
    /// Single insert or batch insert with one SaveChangesAsync; no unbounded in-memory queue.
    /// </summary>
    public class AuditLogService : IAuditLogService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public AuditLogService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task LogAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            if (entry == null) return;
            await LogBatchAsync(new[] { entry }, cancellationToken).ConfigureAwait(false);
        }

        public async Task LogBatchAsync(IEnumerable<AuditLog> entries, CancellationToken cancellationToken = default)
        {
            if (entries == null) return;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService(typeof(ApplicationDbContext)) as ApplicationDbContext;
            if (context == null) return;

            var list = entries as IList<AuditLog> ?? new List<AuditLog>(entries);
            if (list.Count == 0) return;

            foreach (var e in list)
            {
                if (e.ActionDateTime == default)
                    e.ActionDateTime = DateTime.UtcNow;
                context.AuditLogs.Add(e);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
