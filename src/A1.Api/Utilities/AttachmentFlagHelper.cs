using A1.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace A1.Api.Utilities
{
    public static class AttachmentFlagHelper
    {
        public static async Task<HashSet<int>> GetAttachedFormIdsAsync(
            ApplicationDbContext context,
            IEnumerable<int> formIds,
            params string[] formNames)
        {
            var idList = formIds.Distinct().ToList();
            if (idList.Count == 0)
            {
                return new HashSet<int>();
            }

            var validNames = formNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();

            var query = context.FileUploads
                .AsNoTracking()
                .Where(f => idList.Contains(f.FormId) && (f.IsDeleted == null || f.IsDeleted == false));

            if (validNames.Count > 0)
            {
                query = query.Where(f => f.FormName != null && validNames.Contains(f.FormName));
            }

            var ids = await query
                .Select(f => f.FormId)
                .Distinct()
                .ToListAsync();

            return ids.ToHashSet();
        }

        public static List<Dictionary<string, object?>> ToDictionariesWithAttachmentFlag<T>(
            IEnumerable<T> items,
            Func<T, int> idSelector,
            ISet<int> attachedIds)
        {
            var result = new List<Dictionary<string, object?>>();
            foreach (var item in items)
            {
                var id = idSelector(item);
                result.Add(ToDictionaryWithAttachmentFlag(item!, attachedIds.Contains(id)));
            }
            return result;
        }

        public static Dictionary<string, object?> ToDictionaryWithAttachmentFlag<T>(T item, bool isAttachment)
        {
            var dict = new Dictionary<string, object?>();
            var props = item!.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                dict[prop.Name] = prop.GetValue(item);
            }

            dict["IsAttachment"] = isAttachment;
            return dict;
        }
    }
}

