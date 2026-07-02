namespace BankingApi.Models.Responses;
public class CardResponse
{
    public Guid Id { get; set; }
    public decimal CreditLimit { get; set; }
    public string CreditLimitCurrency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
