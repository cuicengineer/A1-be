namespace A1.Api.Models
{
    public class IncomeStatementsReorderRequest
    {
        public int Id { get; set; }
        public string Direction { get; set; } = string.Empty;
    }
}
