namespace BankingApi.Models.Entities;

public enum TransactionType { Purchase, Refund }
public enum TransactionStatus { Approved, Declined }

public class Transaction
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal AmountInCardCurrency { get; set; }
    public string Description { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public string? DeclineReason { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
