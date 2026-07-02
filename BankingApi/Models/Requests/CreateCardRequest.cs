using System.ComponentModel.DataAnnotations;
using BankingApi.Validation;

namespace BankingApi.Models.Requests;

public class CreateCardRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Credit limit must be greater than zero.")]
    public decimal CreditLimit { get; set; }
    [StringLength(10, MinimumLength = 3)][SupportedCurrency]
    public string CreditLimitCurrency { get; set; } = "USD";
}
