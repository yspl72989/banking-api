using BankingApi.Currency;
using BankingApi.Services.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BankingApi.Services;

/// <summary>
/// Adapter wrapping the US Treasury Reporting Rates of Exchange API.
/// Endpoint: GET https://api.fiscaldata.treasury.gov/services/api/fiscal_service/v1/accounting/od/rates_of_exchange
///
/// Accepts ISO 4217 codes (AUD, EUR, GBP ...) and maps them to Treasury format internally.
/// USD pivot logic is also fully encapsulated here — callers receive a direct from→to rate.
/// </summary>
public class TreasuryFxService : IFxService
{
    // Relative to BaseAddress — must NOT start with '/' or HttpClient drops the base path.
    private const string RatesEndpoint = "v1/accounting/od/rates_of_exchange";
    private const string Fields = "country_currency_desc,exchange_rate,record_date";

    private readonly HttpClient _httpClient;
    private readonly ILogger<TreasuryFxService> _logger;

    public TreasuryFxService(HttpClient httpClient, ILogger<TreasuryFxService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExchangeRate?> GetHistoricalRateAsync(
        string fromCurrencyIso, string toCurrencyIso, DateOnly date)
    {
        if (AreSame(fromCurrencyIso, toCurrencyIso))
            return new ExchangeRate(fromCurrencyIso, toCurrencyIso, 1m, date);

        // Treasury rates = foreign units per 1 USD. USD is the pivot — never fetched from Treasury.
        var (directRate, recordDate) = await ResolveDirectRateAsync(fromCurrencyIso, toCurrencyIso, date);
        if (directRate == null) return null;

        _logger.LogInformation(
            "Historical rate {From}→{To} on {Date}: {Rate}",
            fromCurrencyIso, toCurrencyIso, date, directRate);

        return new ExchangeRate(fromCurrencyIso, toCurrencyIso, directRate.Value, recordDate!.Value);
    }

    /// <inheritdoc/>
    public async Task<ExchangeRate?> GetLatestRateAsync(
        string fromCurrencyIso, string toCurrencyIso)
    {
        if (AreSame(fromCurrencyIso, toCurrencyIso))
            return new ExchangeRate(fromCurrencyIso, toCurrencyIso, 1m, DateOnly.FromDateTime(DateTime.UtcNow));

        var (directRate, recordDate) = await ResolveDirectRateAsync(fromCurrencyIso, toCurrencyIso, onOrBefore: null);
        if (directRate == null) return null;

        _logger.LogInformation(
            "Latest rate {From}→{To}: {Rate} (dated {Date})",
            fromCurrencyIso, toCurrencyIso, directRate, recordDate);

        return new ExchangeRate(fromCurrencyIso, toCurrencyIso, directRate.Value, recordDate!.Value);
    }

    /// <summary>
    /// Resolves a direct from→to rate using USD as pivot. Only non-USD legs hit the Treasury API.
    /// </summary>
    private async Task<(decimal? Rate, DateOnly? RecordDate)> ResolveDirectRateAsync(
        string fromCurrencyIso, string toCurrencyIso, DateOnly? onOrBefore)
    {
        if (IsUsd(fromCurrencyIso))
        {
            var toVsUsd = await FetchVsUsdAsync(toCurrencyIso, onOrBefore);
            return toVsUsd == null ? (null, null) : (toVsUsd.Rate, toVsUsd.RecordDate);
        }

        if (IsUsd(toCurrencyIso))
        {
            var fromVsUsd = await FetchVsUsdAsync(fromCurrencyIso, onOrBefore);
            return fromVsUsd == null ? (null, null) : (1m / fromVsUsd.Rate, fromVsUsd.RecordDate);
        }

        var fromVsUsdCross = await FetchVsUsdAsync(fromCurrencyIso, onOrBefore);
        if (fromVsUsdCross == null) return (null, null);

        var toVsUsdCross = await FetchVsUsdAsync(toCurrencyIso, onOrBefore);
        if (toVsUsdCross == null) return (null, null);

        return (toVsUsdCross.Rate / fromVsUsdCross.Rate, toVsUsdCross.RecordDate);
    }

    private async Task<RateResult?> FetchVsUsdAsync(string isoCode, DateOnly? onOrBefore)
    {
        if (IsUsd(isoCode))
            return new RateResult(1m, onOrBefore ?? DateOnly.FromDateTime(DateTime.UtcNow));

        if (!SupportedCurrencies.TryGetTreasuryName(isoCode, out var treasuryName))
            throw new ArgumentException(
                $"Currency '{isoCode}' is not supported. Use ISO 4217 codes (e.g. AUD, EUR, GBP, USD). " +
                "See README for the full list of supported currencies.");

        var filter = onOrBefore.HasValue ? $"record_date:lte:{onOrBefore.Value:yyyy-MM-dd}" : null;
        var url = BuildUrl(treasuryName, filter);

        _logger.LogInformation("Calling Treasury API for {Iso} ({Treasury})", isoCode, treasuryName);

        var entry = await FetchFirstEntryAsync(url);
        if (entry == null) return null;

        var recordDate = DateOnly.Parse(entry.RecordDate);

        if (onOrBefore.HasValue)
        {
            var sixMonthCutoff = onOrBefore.Value.AddMonths(-6);
            if (recordDate < sixMonthCutoff)
            {
                _logger.LogWarning(
                    "Rate for {Iso} on {RecordDate} is outside the 6-month window before {Date}",
                    isoCode, recordDate, onOrBefore);
                return null;
            }
        }

        return new RateResult(decimal.Parse(entry.ExchangeRate), recordDate);
    }

    private static bool IsUsd(string isoCode) =>
        string.Equals(isoCode, "USD", StringComparison.OrdinalIgnoreCase);

    private static bool AreSame(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private string BuildUrl(string treasuryName, string? filter)
    {
        var currencyFilter = $"country_currency_desc:eq:{Uri.EscapeDataString(treasuryName)}";
        var combined = filter != null ? $"{currencyFilter},{filter}" : currencyFilter;
        return $"{RatesEndpoint}?fields={Fields}&filter={combined}&sort=-record_date&page[size]=1";
    }

    private async Task<TreasuryRateEntry?> FetchFirstEntryAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TreasuryResponse>(json, JsonOptions);
            return result?.Data?.FirstOrDefault();
        }
        catch (ArgumentException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve exchange rate from Treasury API. URL: {Url}", url);
            throw new InvalidOperationException(
                "Unable to retrieve exchange rate from the Treasury API. Please try again later.", ex);
        }
    }

    private sealed record RateResult(decimal Rate, DateOnly RecordDate);

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private sealed class TreasuryResponse
    {
        public List<TreasuryRateEntry> Data { get; set; } = new();
    }

    private sealed class TreasuryRateEntry
    {
        [JsonPropertyName("country_currency_desc")]
        public string CountryCurrencyDesc { get; set; } = string.Empty;
        [JsonPropertyName("exchange_rate")]
        public string ExchangeRate { get; set; } = string.Empty;
        [JsonPropertyName("record_date")]
        public string RecordDate { get; set; } = string.Empty;
    }
}
