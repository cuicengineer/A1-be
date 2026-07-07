using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class ProductService : BaseEntity
    {
        [MaxLength(50)]
        public string ItemCode { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ItemName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Uom { get; set; }

        public int? SaleAccountCoaId { get; set; }
        public int? SaleAccountIncomeStatementId { get; set; }
        public int? PurchaseAccountCoaId { get; set; }
        public int? PurchaseAccountIncomeStatementId { get; set; }

        [MaxLength(500)]
        public string? DefaultParticulars { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultUnitPriceSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultUnitPricePurchase { get; set; }

        public int? TaxCodeId { get; set; }

        [MaxLength(20)]
        public string? Status { get; set; }

        [NotMapped]
        public string? SaleAccountDisplay { get; set; }

        [NotMapped]
        public string? PurchaseAccountDisplay { get; set; }

        [NotMapped]
        public string? TaxCodeDisplay { get; set; }
    }
}
