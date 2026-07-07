namespace A1.Api.Models
{
    public class ShareDistributionRow
    {
        public int SN { get; set; }
        public int Id { get; set; }
        public string? ContractNo { get; set; }
        public string? RACName { get; set; }
        public string? Base { get; set; }
        public string? Class { get; set; }
        public string? Agreement { get; set; }
        public string? TenantAndBusiness { get; set; }
        public decimal? BoOArea { get; set; }
        public string? RRFY { get; set; }
        public decimal? RevenueRate { get; set; }
        public decimal? GovtSharePA { get; set; }
        public decimal? CurrentRentPA { get; set; }
        public DateTime? ReceiptDate { get; set; }
        public decimal? ReceiptAmount { get; set; }
        public decimal? Ratio { get; set; }
        public decimal? Govt { get; set; }
        public decimal? PAF { get; set; }
        public decimal? AHQ { get; set; }
        public decimal? RAC { get; set; }
        public decimal? BaseShare { get; set; }
        public string? Workbook { get; set; }
        public string? CAId { get; set; }
        public decimal? CAArea1 { get; set; }
        public decimal? CAArea2 { get; set; }
    }
}
