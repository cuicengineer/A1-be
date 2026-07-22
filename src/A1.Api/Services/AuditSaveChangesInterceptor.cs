using A1.Api.Models;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace A1.Api.Services
{
    /// <summary>
    /// Automatically writes AuditLog rows for every Modified entity on SaveChanges
    /// (covers all PUT/edit paths that persist via EF).
    /// </summary>
    public class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "Password",
            "PasswordSalt",
            "PlainPassword",
            "RefreshToken",
            "PasswordHash",
            "PasswordIterations"
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private readonly ConcurrentDictionary<DbContextId, List<AuditLog>> _pendingByContext = new();
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditSaveChangesInterceptor(IServiceScopeFactory scopeFactory, IHttpContextAccessor httpContextAccessor)
        {
            _scopeFactory = scopeFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            CollectAuditsAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
            return base.SavingChanges(eventData, result);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await CollectAuditsAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
            return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            FlushAuditsAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
            return base.SavedChanges(eventData, result);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            await FlushAuditsAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
            return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            if (eventData.Context != null)
                _pendingByContext.TryRemove(eventData.Context.ContextId, out _);
            base.SaveChangesFailed(eventData);
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null)
                _pendingByContext.TryRemove(eventData.Context.ContextId, out _);
            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        private async Task CollectAuditsAsync(DbContext? context, CancellationToken cancellationToken)
        {
            if (context == null) return;

            var pending = new List<AuditLog>();
            var httpContext = _httpContextAccessor.HttpContext;
            var actionBy = ActionByHelper.GetActionByWithIp(httpContext?.User, httpContext);
            var now = DateTime.UtcNow;

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Modified) continue;
                if (entry.Entity is AuditLog) continue;

                var entityName = entry.Metadata.ClrType.Name;
                if (string.Equals(entityName, nameof(AuditLog), StringComparison.OrdinalIgnoreCase))
                    continue;

                Dictionary<string, object?> oldValues;
                Dictionary<string, object?> newValues;

                try
                {
                    var dbValues = await entry.GetDatabaseValuesAsync(cancellationToken).ConfigureAwait(false);
                    oldValues = dbValues != null
                        ? ToAuditDictionary(dbValues)
                        : ToAuditDictionary(entry.OriginalValues);
                    newValues = ToAuditDictionary(entry.CurrentValues);
                }
                catch
                {
                    oldValues = ToAuditDictionary(entry.OriginalValues);
                    newValues = ToAuditDictionary(entry.CurrentValues);
                }

                if (DictionariesEqual(oldValues, newValues))
                    continue;

                pending.Add(new AuditLog
                {
                    EntityName = entityName,
                    EntityId = TryGetEntityId(entry),
                    OldValuesJson = JsonSerializer.Serialize(oldValues, JsonOptions),
                    NewValuesJson = JsonSerializer.Serialize(newValues, JsonOptions),
                    ActionBy = actionBy,
                    Action = "API",
                    ActionDateTime = now
                });
            }

            if (pending.Count > 0)
                _pendingByContext[context.ContextId] = pending;
            else
                _pendingByContext.TryRemove(context.ContextId, out _);
        }

        private async Task FlushAuditsAsync(DbContext? context, CancellationToken cancellationToken)
        {
            if (context == null) return;
            if (!_pendingByContext.TryRemove(context.ContextId, out var pending) || pending.Count == 0)
                return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var auditLogService = scope.ServiceProvider.GetService<IAuditLogService>();
                if (auditLogService == null) return;
                await auditLogService.LogBatchAsync(pending, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Never fail the business save because audit logging failed.
            }
        }

        private static Dictionary<string, object?> ToAuditDictionary(PropertyValues values)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in values.Properties)
            {
                if (SensitiveProperties.Contains(prop.Name)) continue;
                var value = values[prop];
                if (value is byte[] bytes)
                    dict[prop.Name] = Convert.ToBase64String(bytes);
                else
                    dict[prop.Name] = value;
            }
            return dict;
        }

        private static long? TryGetEntityId(EntityEntry entry)
        {
            try
            {
                var prop = entry.Properties.FirstOrDefault(p =>
                    string.Equals(p.Metadata.Name, "Id", StringComparison.OrdinalIgnoreCase));
                if (prop?.CurrentValue == null) return null;
                return Convert.ToInt64(prop.CurrentValue);
            }
            catch
            {
                return null;
            }
        }

        private static bool DictionariesEqual(Dictionary<string, object?> a, Dictionary<string, object?> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out var other)) return false;
                if (!EqualsNormalized(kv.Value, other)) return false;
            }
            return true;
        }

        private static bool EqualsNormalized(object? left, object? right)
        {
            if (left == null && right == null) return true;
            if (left == null || right == null) return false;
            if (left is DateTime ld && right is DateTime rd)
                return ld.Ticks == rd.Ticks;
            return Equals(left, right) || string.Equals(Convert.ToString(left), Convert.ToString(right), StringComparison.Ordinal);
        }
    }
}
