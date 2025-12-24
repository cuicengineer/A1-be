using System;

namespace A1.Api.Models
{
    public class RentalProperty : BaseEntity
    {
        public int CmdId { get; set; }
        public int BaseId { get; set; }
        public int ClassId { get; set; }
        public string? PId { get; set; }
        public string? UoM { get; set; }
        public decimal? Area { get; set; }
        public string? Location { get; set; }
        public string? Remarks { get; set; }
        public bool? Status { get; set; }
    }
}