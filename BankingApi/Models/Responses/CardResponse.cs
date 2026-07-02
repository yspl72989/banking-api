namespace BankingApi.Models.Responses;

public class CardResponse
{
    public Guid Id { get; set; }
    public decimal CreditLimit { get; set; }
    public DateTime CreatedAt { get; set; }
}
