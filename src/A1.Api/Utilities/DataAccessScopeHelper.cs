using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using A1.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Utilities
{
    public sealed class DataAccessScope
    {
        public string AccessLevel { get; init; } = "ahq"; // ahq | command | base
        public bool IsAhq => string.Equals(AccessLevel, "ahq", StringComparison.OrdinalIgnoreCase);
        public int? CmdId { get; init; }
        public int? BaseId { get; init; }
        public IReadOnlyCollection<int> AllowedBaseIds { get; init; } = Array.Empty<int>();
    }

    public static class DataAccessScopeHelper
    {
        public static async Task<DataAccessScope> ResolveAsync(ClaimsPrincipal principal, ApplicationDbContext context)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return new DataAccessScope { AccessLevel = "base", CmdId = null, BaseId = null };
            }

            var userId = TryParseIntClaim(principal, JwtRegisteredClaimNames.Sub)
                         ?? TryParseIntClaim(principal, ClaimTypes.NameIdentifier);

            var username = principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                           ?? principal.FindFirstValue(ClaimTypes.Name)
                           ?? principal.Identity?.Name
                           ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            User? user = null;
            if (userId.HasValue)
            {
                user = await context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);
            }

            if (user == null && !string.IsNullOrWhiteSpace(username))
            {
                user = await context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Username == username);
            }

            var roleName = principal.FindFirstValue(ClaimTypes.Role) ?? principal.FindFirstValue("role");
            if (string.IsNullOrWhiteSpace(roleName) && user?.LevelId != null)
            {
                roleName = await context.Roles
                    .AsNoTracking()
                    .Where(r => r.Id == user.LevelId.Value && (r.IsDeleted == null || r.IsDeleted == false))
                    .Select(r => r.RoleName)
                    .FirstOrDefaultAsync();
            }

            var normalized = NormalizeAccessLevel(roleName, user);
            var allowedBaseIds = Array.Empty<int>();
            if (string.Equals(normalized, "command", StringComparison.OrdinalIgnoreCase) && user?.CmdId != null)
            {
                allowedBaseIds = await context.Bases
                    .AsNoTracking()
                    .Where(b => b.Cmd == user.CmdId.Value && (b.IsDeleted == null || b.IsDeleted == false))
                    .Select(b => b.Id)
                    .ToArrayAsync();
            }

            return new DataAccessScope
            {
                AccessLevel = normalized,
                CmdId = user?.CmdId,
                BaseId = user?.BaseId,
                AllowedBaseIds = allowedBaseIds
            };
        }

        public static IQueryable<TEntity> ApplyScope<TEntity>(IQueryable<TEntity> query, DataAccessScope scope) where TEntity : class
        {
            if (scope.IsAhq)
            {
                return query;
            }

            if (string.Equals(scope.AccessLevel, "command", StringComparison.OrdinalIgnoreCase))
            {
                query = ApplyIntPropertyFilter(query, "CmdId", scope.CmdId);
                if (scope.BaseId.HasValue)
                {
                    query = ApplyIntPropertyFilter(query, "BaseId", scope.BaseId);
                }
                else
                {
                    query = ApplyIntPropertyInFilter(query, "BaseId", scope.AllowedBaseIds);
                }
                return query;
            }

            // Base-level default: only base data
            query = ApplyIntPropertyFilter(query, "BaseId", scope.BaseId);
            return query;
        }

        public static IQueryable ApplyScope(IQueryable query, Type entityType, DataAccessScope scope)
        {
            if (scope.IsAhq)
            {
                return query;
            }

            if (string.Equals(scope.AccessLevel, "command", StringComparison.OrdinalIgnoreCase))
            {
                query = ApplyIntPropertyFilter(query, entityType, "CmdId", scope.CmdId);
                if (scope.BaseId.HasValue)
                {
                    query = ApplyIntPropertyFilter(query, entityType, "BaseId", scope.BaseId);
                }
                else
                {
                    query = ApplyIntPropertyInFilter(query, entityType, "BaseId", scope.AllowedBaseIds);
                }
                return query;
            }

            return ApplyIntPropertyFilter(query, entityType, "BaseId", scope.BaseId);
        }

        private static IQueryable<TEntity> ApplyIntPropertyFilter<TEntity>(IQueryable<TEntity> query, string propertyName, int? value) where TEntity : class
        {
            var prop = typeof(TEntity).GetProperty(propertyName);
            if (prop == null)
            {
                return query;
            }

            if (!value.HasValue)
            {
                return query.Where(_ => false);
            }

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var property = Expression.Property(parameter, prop);
            var constant = Expression.Constant(value.Value);

            Expression comparison = prop.PropertyType == typeof(int?)
                ? Expression.Equal(property, Expression.Convert(constant, typeof(int?)))
                : Expression.Equal(property, constant);

            var lambda = Expression.Lambda<Func<TEntity, bool>>(comparison, parameter);
            return query.Where(lambda);
        }

        private static IQueryable ApplyIntPropertyFilter(IQueryable query, Type entityType, string propertyName, int? value)
        {
            var prop = entityType.GetProperty(propertyName);
            if (prop == null)
            {
                return query;
            }

            if (!value.HasValue)
            {
                var alwaysFalseParam = Expression.Parameter(entityType, "e");
                var falseLambda = Expression.Lambda(Expression.Constant(false), alwaysFalseParam);
                return ApplyWhere(query, entityType, falseLambda);
            }

            var parameter = Expression.Parameter(entityType, "e");
            var property = Expression.Property(parameter, prop);
            var constant = Expression.Constant(value.Value);

            Expression comparison = prop.PropertyType == typeof(int?)
                ? Expression.Equal(property, Expression.Convert(constant, typeof(int?)))
                : Expression.Equal(property, constant);

            var lambda = Expression.Lambda(comparison, parameter);
            return ApplyWhere(query, entityType, lambda);
        }

        private static IQueryable<TEntity> ApplyIntPropertyInFilter<TEntity>(IQueryable<TEntity> query, string propertyName, IReadOnlyCollection<int> values) where TEntity : class
        {
            var prop = typeof(TEntity).GetProperty(propertyName);
            if (prop == null)
            {
                return query;
            }

            if (values == null || values.Count == 0)
            {
                return query.Where(_ => false);
            }

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var property = Expression.Property(parameter, prop);
            var valuesConstant = Expression.Constant(values.ToList());
            var containsMethod = typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!;

            Expression itemExpr = prop.PropertyType == typeof(int?)
                ? Expression.Coalesce(property, Expression.Constant(int.MinValue))
                : property;

            var containsExpr = Expression.Call(valuesConstant, containsMethod, itemExpr);
            var lambda = Expression.Lambda<Func<TEntity, bool>>(containsExpr, parameter);
            return query.Where(lambda);
        }

        private static IQueryable ApplyIntPropertyInFilter(IQueryable query, Type entityType, string propertyName, IReadOnlyCollection<int> values)
        {
            var prop = entityType.GetProperty(propertyName);
            if (prop == null)
            {
                return query;
            }

            if (values == null || values.Count == 0)
            {
                var alwaysFalseParam = Expression.Parameter(entityType, "e");
                var falseLambda = Expression.Lambda(Expression.Constant(false), alwaysFalseParam);
                return ApplyWhere(query, entityType, falseLambda);
            }

            var parameter = Expression.Parameter(entityType, "e");
            var property = Expression.Property(parameter, prop);
            var valuesList = values.ToList();
            var valuesConstant = Expression.Constant(valuesList);
            var containsMethod = typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!;

            Expression itemExpr = prop.PropertyType == typeof(int?)
                ? Expression.Coalesce(property, Expression.Constant(int.MinValue))
                : property;

            var containsExpr = Expression.Call(valuesConstant, containsMethod, itemExpr);
            var lambda = Expression.Lambda(containsExpr, parameter);
            return ApplyWhere(query, entityType, lambda);
        }

        private static IQueryable ApplyWhere(IQueryable query, Type entityType, LambdaExpression predicate)
        {
            var whereMethod = typeof(Queryable)
                .GetMethods()
                .First(m => m.Name == nameof(Queryable.Where) && m.GetParameters().Length == 2)
                .MakeGenericMethod(entityType);

            return (IQueryable)whereMethod.Invoke(null, new object[] { query, predicate })!;
        }

        private static int? TryParseIntClaim(ClaimsPrincipal principal, string claimType)
        {
            var raw = principal.FindFirstValue(claimType);
            return int.TryParse(raw, out var value) ? value : null;
        }

        private static string NormalizeAccessLevel(string? roleName, User? user)
        {
            var role = (roleName ?? string.Empty).Trim().ToLowerInvariant();
            if (role.Contains("ahq") || role == "hquser")
            {
                return "ahq";
            }

            if (role.Contains("command") || role.Contains("cmd"))
            {
                return "command";
            }

            if (role.Contains("base"))
            {
                return "base";
            }

            // Fallbacks when role text is not available.
            if (user?.LevelId == 1) return "ahq";
            if (user?.LevelId == 2) return "command";
            if (user?.LevelId == 3) return "base";

            if (user?.CmdId != null && user?.BaseId != null) return "command";
            if (user?.BaseId != null) return "base";

            return "ahq";
        }
    }
}

