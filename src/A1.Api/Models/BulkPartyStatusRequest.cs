using System.Collections.Generic;

namespace A1.Api.Models
{
    public class BulkPartyStatusRequest
    {
        public List<int> Ids { get; set; } = new();
        public bool Status { get; set; }
    }
}
