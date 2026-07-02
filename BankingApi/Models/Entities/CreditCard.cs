namespace BankingApi.Models.Entities;

public class CreditCard
{
    public Guid Id { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public string Cvv { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public decimal AvailableCredit { get; set; }
    public string Currency { get; set; } = "AUD";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
