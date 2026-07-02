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
    private const string RatesEndpoint = "/v1/accounting/od/rates_of_exchange";
    private const string Fields = "country_currency_desc,exchange_rate,record_date";

    /// <summary>ISO 4217 → Treasury API country_currency_desc.</summary>
    private static readonly Dictionary<string, string> IsoToTreasury =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["AUD"] = "Australia-Dollar",
            ["EUR"] = "Euro Zone-Euro",
            ["GBP"] = "United Kingdom-Pound",
            ["CAD"] = "Canada-Dollar",
            ["JPY"] = "Japan-Yen",
            ["NZD"] = "New Zealand-Dollar",
            ["CHF"] = "Switzerland-Franc",
            ["SGD"] = "Singapore-Dollar",
            ["HKD"] = "Hong Kong-Dollar",
            ["MXN"] = "Mexico-Peso",
            ["CNY"] = "China-Renminbi",
            ["KRW"] = "Korea-Won",
            ["BRL"] = "Brazil-Real",
            ["INR"] = "India-Rupee",
            ["SEK"] = "Sweden-Krona",
            ["NOK"] = "Norway-Krone",
            ["DKK"] = "Denmark-Krone",
            ["THB"] = "Thailand-Baht",
            ["MYR"] = "Malaysia-Ringgit",
        };

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

        // 6-month rule: recordDate >= date.AddMonths(-6) AND recordDate <= date (calendar months, inclusive)
        var fromResult = await FetchVsUsdAsync(fromCurrencyIso, onOrBefore: date);
        if (fromResult == null) return null;

        var toResult = await FetchVsUsdAsync(toCurrencyIso, onOrBefore: date);
        if (toResult == null) return null;

        // Treasury rates = units of foreign per 1 USD → from→to via pivot: toRate / fromRate
        var directRate = toResult.Rate / fromResult.Rate;

        _logger.LogInformation(
            "Historical rate {From}→{To} on {Date}: {Rate}",
            fromCurrencyIso, toCurrencyIso, date, directRate);

        return new ExchangeRate(fromCurrencyIso, toCurrencyIso, directRate, toResult.RecordDate);
    }

    /// <inheritdoc/>
    public async Task<ExchangeRate?> GetLatestRateAsync(
        string fromCurrencyIso, string toCurrencyIso)
    {
        if (AreSame(fromCurrencyIso, toCurrencyIso))
            return new ExchangeRate(fromCurrencyIso, toCurrencyIso, 1m, DateOnly.FromDateTime(DateTime.UtcNow));

        var fromResult = await FetchVsUsdAsync(fromCurrencyIso, onOrBefore: null);
        if (fromResult == null) return null;

        var toResult = await FetchVsUsdAsync(toCurrencyIso, onOrBefore: null);
        if (toResult == null) return null;

        var directRate = toResult.Rate / fromResult.Rate;

        _logger.LogInformation(
            "Latest rate {From}→{To}: {Rate} (dated {Date})",
            fromCurrencyIso, toCurrencyIso, directRate, toResult.RecordDate);

        return new ExchangeRate(fromCurrencyIso, toCurrencyIso, directRate, toResult.RecordDate);
    }

    private async Task<RateResult?> FetchVsUsdAsync(string isoCode, DateOnly? onOrBefore)
    {
        if (IsUsd(isoCode))
            return new RateResult(1m, onOrBefore ?? DateOnly.FromDateTime(DateTime.UtcNow));

        if (!IsoToTreasury.TryGetValue(isoCode, out var treasuryName))
            throw new ArgumentException(
                $"Currency '{isoCode}' is not supported. Use ISO 4217 codes (e.g. AUD, EUR, GBP). " +
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
