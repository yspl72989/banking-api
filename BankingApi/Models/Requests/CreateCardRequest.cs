using System.ComponentModel.DataAnnotations;

namespace BankingApi.Models.Requests;

public class CreateCardRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string CardHolderName { get; set; } = string.Empty;

    [Required]
    [Range(100, 100000, ErrorMessage = "Credit limit must be between 100 and 100,000.")]
    public decimal CreditLimit { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-letter ISO code, e.g. AUD.")]
    public string Currency { get; set; } = "AUD";
}
