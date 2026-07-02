namespace BankingApi.Domain;

public class Card
{
    public Guid Id { get; set; }
    public decimal CreditLimit { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
