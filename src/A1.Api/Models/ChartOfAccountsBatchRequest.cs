using System.Collections.Generic;

namespace A1.Api.Models
{
    public class ChartOfAccountsBatchRequest
    {
        public List<ChartOfAccount> Creates { get; set; } = new();
        public List<ChartOfAccount> Updates { get; set; } = new();
        public List<int> DeleteIds { get; set; } = new();
    }
}
