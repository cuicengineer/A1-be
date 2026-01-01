using System;

namespace A1.Api.Models
{
    public class PropertyGroupLinking : BaseEntity
    {
        public int GrpId { get; set; }
        public int PropId { get; set; }
        public decimal? Area { get; set; }
        public bool? Status { get; set; }
    }
}

