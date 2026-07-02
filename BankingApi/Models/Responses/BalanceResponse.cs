namespace BankingApi.Models.Responses;
public class BalanceResponse
{
    public Guid CardId { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal TotalTransactions { get; set; }
    public decimal AvailableBalance { get; set; }
    public string Currency { get; set; } = string.Empty;
}
