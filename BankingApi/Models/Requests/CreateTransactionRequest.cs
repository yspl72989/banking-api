using System.ComponentModel.DataAnnotations;

namespace BankingApi.Models.Requests;

public class CreateTransactionRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateOnly TransactionDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }
}
