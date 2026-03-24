using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace A1.Api.Utilities
{
    /// <summary>
    /// Central helper for ActionBy values: resolves current user and client IP from the request (no third-party calls).
    /// Use when persisting ActionBy for user tracking.
    /// </summary>
    public static class ActionByHelper
    {
        /// <summary>
        /// Returns true only if the value is a valid IPv4 or IPv6 address (rejects session IDs, hostnames, etc.).
        /// </summary>
        private static bool IsValidIp(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            value = value.Trim();
            return IPAddress.TryParse(value, out _);
        }

        /// <summary>
        /// Gets client IP from the HTTP request only (X-Forwarded-For, X-Real-IP, then RemoteIpAddress).
        /// Only values that parse as valid IP addresses are accepted; session IDs or other non-IP values are skipped.
        /// </summary>
        public static string GetClientIp(HttpContext? context)
        {
            if (context?.Request == null) return string.Empty;

            // X-Forwarded-For: leftmost is original client; each value must be a valid IP (ignore session IDs etc.)
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                foreach (var part in forwarded.Split(','))
                {
                    var candidate = part.Trim();
                    if (IsValidIp(candidate)) return candidate;
                }
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(realIp))
            {
                var candidate = realIp.Trim();
                if (IsValidIp(candidate)) return candidate;
            }

            var remoteIp = context.Connection?.RemoteIpAddress;

            if (remoteIp != null)
            {
                if (remoteIp.IsIPv4MappedToIPv6)
                    remoteIp = remoteIp.MapToIPv4();

                var ip = remoteIp.ToString();
                if (IsValidIp(ip))
                    return ip;
            }

            return string.Empty;
        }

        /// <summary>
        /// Resolves the current user identifier from claims (no IP).
        /// </summary>
        public static string GetActionBy(ClaimsPrincipal? user)
        {
            var name = user?.Identity?.Name;
            if (!string.IsNullOrEmpty(name)) return name;
            var claim = user?.FindFirst(ClaimTypes.Name) ?? user?.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && !string.IsNullOrEmpty(claim.Value)) return claim.Value;
            return "System";
        }

        /// <summary>
        /// Normalizes an incoming ActionBy payload by removing any appended IP segment(s).
        /// This prevents repeated values like "user|ip|ip|ip" when old ActionBy is reused.
        /// </summary>
        private static string NormalizeActionByUserPart(ClaimsPrincipal? user, string? userPart)
        {
            var fallback = GetActionBy(user);
            if (string.IsNullOrWhiteSpace(userPart)) return fallback;

            var raw = userPart.Trim();
            var segments = raw
                .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0) return fallback;

            // Keep only the leading non-IP portion and discard trailing IP segment(s).
            var kept = new List<string>();
            foreach (var segment in segments)
            {
                if (IsValidIp(segment))
                {
                    break;
                }
                kept.Add(segment);
            }

            var normalized = kept.Count > 0 ? string.Join("|", kept) : segments[0];
            if (string.IsNullOrWhiteSpace(normalized) || IsValidIp(normalized))
            {
                return fallback;
            }

            return normalized;
        }

        /// <summary>
        /// Returns value to store in ActionBy column: user part (from override or claims) and client IP, e.g. "user|192.168.1.1".
        /// </summary>
        /// <param name="user">Current ClaimsPrincipal.</param>
        /// <param name="context">Current HttpContext (for client IP).</param>
        /// <param name="userPart">Optional user string from payload; if null, resolved from claims.</param>
        public static string GetActionByWithIp(ClaimsPrincipal? user, HttpContext? context, string? userPart = null)
        {
            var part = NormalizeActionByUserPart(user, userPart);
            var ip = GetClientIp(context);
            return string.IsNullOrEmpty(ip) ? part : $"{part}|{ip}";
        }
    }
}
