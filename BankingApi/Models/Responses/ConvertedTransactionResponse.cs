namespace BankingApi.Models.Responses;
public class ConvertedTransactionResponse
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal ConvertedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateOnly ExchangeRateDate { get; set; }
}
