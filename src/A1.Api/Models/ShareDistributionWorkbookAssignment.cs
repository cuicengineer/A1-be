namespace A1.Api.Models
{
    public class ShareDistributionWorkbookAssignment : BaseEntity
    {
        public int ContractId { get; set; }
        public string WorkbookNo { get; set; } = string.Empty;
        public int WorkbookSerial { get; set; }
        public DateTime WorkbookCreatedDate { get; set; }
    }
}
