namespace BankingApi.Services.Contracts;

public interface IFxService
{
    /// <summary>
    /// Returns the exchange rate to convert <paramref name="fromCurrency"/> into <paramref name="toCurrency"/>.
    /// Returns 1 if both currencies are the same.
    /// </summary>
    Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency);
}
