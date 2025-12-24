using A1.Api.Models;
using System;
using System.Text.Json.Serialization;
using A1.Api.Converters;

namespace A1.Api.Models
{
    public class Nature : BaseEntity
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public byte? Status { get; set; }
        [JsonConverter(typeof(DecimalConverter))]
        public decimal? RentalVal { get; set; }
        [JsonConverter(typeof(DecimalConverter))]
        public decimal? AnnualRent { get; set; }
        public byte? GovtShare { get; set; }
        public byte? PAFShare { get; set; }
        public string? PropNumber { get; set; }
    }
}