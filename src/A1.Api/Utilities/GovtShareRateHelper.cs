using A1.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Utilities
{
    /// <summary>
    /// Resolves latest active Govt Share rate (RentalValueGovtShareRates, type=2) per RAC/Base/Class scope.
    /// Mirrors contracts/property-grouping frontend logic for Factor (Config) checks.
    /// </summary>
    public static class GovtShareRateHelper
    {
        private const int GovtShareRateType = 2;

        public static bool IsAnnualRentFactor(string? config)
        {
            var normalized = (config ?? string.Empty).Trim();
            return string.Equals(normalized, "Annual Rent", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Annual Rate", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns class IDs (for the given RAC/Base) whose latest active applicable Govt Share rate uses Annual Rent factor.
        /// </summary>
        public static async Task<HashSet<int>> GetClassIdsWithAnnualRentFactorAsync(
            ApplicationDbContext context,
            int cmdId,
            int baseId,
            IEnumerable<int> classIds,
            DateTime today,
            CancellationToken cancellationToken = default)
        {
            var classIdList = classIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (classIdList.Count == 0)
            {
                return new HashSet<int>();
            }

            var rows = await context.RentalValueGovtShareRates
                .AsNoTracking()
                .Where(r => (r.IsDeleted == null || r.IsDeleted == false)
                            && r.Status == true
                            && r.Type == GovtShareRateType
                            && r.CmdId == cmdId
                            && r.BaseId == baseId
                            && classIdList.Contains(r.ClassId)
                            && r.ApplicableDate != null
                            && r.ApplicableDate.Value.Date <= today)
                .Select(r => new GovtShareRateScopeRow
                {
                    ClassId = r.ClassId,
                    ApplicableDate = r.ApplicableDate!.Value.Date,
                    DeactiveDate = r.DeactiveDate,
                    Config = r.Config
                })
                .ToListAsync(cancellationToken);

            var annualRentClassIds = new HashSet<int>();
            foreach (var group in rows.GroupBy(r => r.ClassId))
            {
                var latest = group
                    .Where(r => !IsDeactivatedOnOrBefore(r.DeactiveDate, today))
                    .OrderByDescending(r => r.ApplicableDate)
                    .FirstOrDefault();

                if (latest != null && IsAnnualRentFactor(latest.Config))
                {
                    annualRentClassIds.Add(group.Key);
                }
            }

            return annualRentClassIds;
        }

        private static bool IsDeactivatedOnOrBefore(DateTime? deactiveDate, DateTime today)
        {
            if (!deactiveDate.HasValue)
            {
                return false;
            }

            return deactiveDate.Value.Date <= today;
        }

        private sealed class GovtShareRateScopeRow
        {
            public int ClassId { get; init; }
            public DateTime ApplicableDate { get; init; }
            public DateTime? DeactiveDate { get; init; }
            public string? Config { get; init; }
        }
    }
}
