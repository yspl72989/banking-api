namespace BankingApi.Models.Responses;

public class TransactionResponse
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal AmountInCardCurrency { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DeclineReason { get; set; }
    public DateTime ProcessedAt { get; set; }
}
