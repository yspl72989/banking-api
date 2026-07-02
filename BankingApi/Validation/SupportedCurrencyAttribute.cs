using System.ComponentModel.DataAnnotations;
using BankingApi.Currency;

namespace BankingApi.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SupportedCurrencyAttribute : ValidationAttribute
{
    public SupportedCurrencyAttribute()
        : base("Currency '{0}' is not supported. Use ISO 4217 codes (e.g. AUD, EUR, GBP, USD).")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string isoCode || string.IsNullOrWhiteSpace(isoCode))
            return ValidationResult.Success;

        return SupportedCurrencies.IsSupported(isoCode)
            ? ValidationResult.Success
            : new ValidationResult(FormatErrorMessage(isoCode));
    }
}
