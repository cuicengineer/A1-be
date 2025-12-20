using System;

namespace A1.Api.Models
{
    public class Product : BaseEntity
    {
        public string? Name { get; set; }
        public decimal Price { get; set; }
    }
}