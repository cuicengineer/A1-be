using System.Linq;

namespace A1.Api.Utilities
{
    public static class PaginationHelper
    {
        public static int NormalizePageNumber(int pageNumber) => pageNumber <= 0 ? 1 : pageNumber;

        /// <summary>
        /// Applies paging when pageSize is greater than zero. pageSize &lt;= 0 returns the full query.
        /// </summary>
        public static IQueryable<T> ApplyPaging<T>(IQueryable<T> query, int pageNumber, int pageSize)
        {
            if (pageSize <= 0)
            {
                return query;
            }

            var normalizedPageNumber = NormalizePageNumber(pageNumber);
            return query
                .Skip((normalizedPageNumber - 1) * pageSize)
                .Take(pageSize);
        }

        public static string FormatPageSizeHeader(int pageSize, int totalCount) =>
            pageSize <= 0 ? totalCount.ToString() : pageSize.ToString();
    }
}
