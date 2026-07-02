namespace BankingApi.Services.Contracts;

/// <summary>
/// Provides direct currency conversion rates using ISO 4217 codes.
/// All Treasury API internals — ISO mapping, USD pivot, rate direction — are
/// encapsulated in the implementing adapter. Callers receive a clean from→to rate.
/// </summary>
public interface IFxService
{
    /// <summary>
    /// Returns the rate to convert 1 unit of <paramref name="fromCurrencyIso"/> into
    /// <paramref name="toCurrencyIso"/>, using the rate active on or before
    /// <paramref name="date"/>, within the prior 6 calendar months (inclusive boundary).
    /// Returns null if no valid rate is available within that window.
    /// </summary>
    Task<ExchangeRate?> GetHistoricalRateAsync(
        string fromCurrencyIso, string toCurrencyIso, DateOnly date);

    /// <summary>
    /// Returns the most recently available rate to convert 1 unit of
    /// <paramref name="fromCurrencyIso"/> into <paramref name="toCurrencyIso"/>.
    /// Returns null if no rate is available for the pair.
    /// </summary>
    Task<ExchangeRate?> GetLatestRateAsync(
        string fromCurrencyIso, string toCurrencyIso);
}

/// <param name="FromCurrency">ISO 4217 code of the source currency (e.g. "EUR").</param>
/// <param name="ToCurrency">ISO 4217 code of the target currency (e.g. "AUD").</param>
/// <param name="Rate">
/// How many units of <see cref="ToCurrency"/> are received for 1 unit of <see cref="FromCurrency"/>.
/// </param>
/// <param name="RecordDate">The effective date of this rate.</param>
public record ExchangeRate(
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateOnly RecordDate);
