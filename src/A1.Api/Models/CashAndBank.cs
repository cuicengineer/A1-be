namespace A1.Api.Models
{
    public class CashAndBank : BaseEntity
    {
        public string? AcctId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? CoaId { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string? IBAN { get; set; }
        public int? BankListsId { get; set; }
        public string? Status { get; set; }
        public int? ParentCashAndBankId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? CoaDisplay { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? BankDisplay { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int ChildCount { get; set; }
    }
}
