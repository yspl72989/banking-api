namespace BankingApi.Domain;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }

    public Card Card { get; set; } = null!;
}
