namespace BankingApi.Models.Responses;

public class TransactionResponse
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}
