using A1.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Utilities
{
    public static class UserDefaultPermissionsHelper
    {
        public static readonly string[] DefaultViewMenuNames =
        {
            "Dashboard",
            "KPI Overview",
            "Guidelines",
        };

        public static async Task SeedDefaultViewPermissionsAsync(
            ApplicationDbContext context,
            int userId,
            string? actionBy = null)
        {
            if (userId <= 0) return;

            var existingMenus = await context.UserPermissions
                .AsNoTracking()
                .Where(p => p.UserId == userId && (p.IsDeleted == null || p.IsDeleted == false))
                .Select(p => p.MenuName)
                .ToListAsync();

            var existingSet = new HashSet<string>(
                existingMenus.Select(m => m.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;
            var added = false;

            foreach (var menuName in DefaultViewMenuNames)
            {
                if (existingSet.Contains(menuName)) continue;

                context.UserPermissions.Add(new UserPermission
                {
                    UserId = userId,
                    MenuName = menuName,
                    CanView = true,
                    CanCreate = false,
                    CanEdit = false,
                    CanDelete = false,
                    IsDeleted = false,
                    Action = "CREATE",
                    ActionBy = actionBy,
                    ActionDate = now,
                });
                added = true;
            }

            if (added)
            {
                await context.SaveChangesAsync();
            }
        }
    }
}
