namespace BankingApi.Services.Contracts;

/// <summary>
/// Adapter over the US Treasury Reporting Rates of Exchange API.
/// Exchange rates are expressed as units of foreign currency per 1 USD.
/// </summary>
public interface IFxService
{
    /// <summary>
    /// Returns the exchange rate for <paramref name="currency"/> on or before
    /// <paramref name="date"/>, within the prior 6 months.
    /// Returns null if no rate is available within that window (Req 3 error case).
    /// </summary>
    Task<ExchangeRate?> GetRateOnOrBeforeDateAsync(string currency, DateOnly date);

    /// <summary>
    /// Returns the most recently published exchange rate for <paramref name="currency"/>.
    /// Returns null if no rate exists for that currency (Req 4 error case).
    /// </summary>
    Task<ExchangeRate?> GetLatestRateAsync(string currency);
}

public record ExchangeRate(decimal Rate, DateOnly RecordDate);
