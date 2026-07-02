namespace BankingApi.Models.Responses;

public class BalanceResponse
{
    public Guid CardId { get; set; }
    public string CardHolderName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public decimal AvailableCredit { get; set; }
    public decimal UsedCredit { get; set; }
    public decimal UtilisationPercent { get; set; }
    public DateTime AsAt { get; set; } = DateTime.UtcNow;
}
