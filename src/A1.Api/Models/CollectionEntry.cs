namespace A1.Api.Models
{
    public class CollectionEntry : BaseEntity
    {
        public int? ClassId { get; set; }
        public int? ContractId { get; set; }
        public string? ContractNo { get; set; }
        public string? TenantNo { get; set; }
        public string? TenantBusiness { get; set; }
        public int? CoaId { get; set; }
        public string? InvoiceNo { get; set; }
        public decimal? ReceivableAmount { get; set; }
        public decimal? DueAmount { get; set; }
        public decimal? BalanceAmount { get; set; }
        public DateTime? CollectionDate { get; set; }
        public decimal? Amount { get; set; }
        public string? TinTrn { get; set; }
        public string? Status { get; set; }
        public string? VrNo { get; set; }
        public DateTime? VrDate { get; set; }
        public int? ReceiptId { get; set; }
    }
}
