using BankingApi.Services.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BankingApi.Services;

/// <summary>
/// Adapter that wraps the US Treasury Reporting Rates of Exchange API.
/// Endpoint: GET /v1/accounting/od/rates_of_exchange
/// Rates are expressed as units of foreign currency per 1 USD and published quarterly.
/// Currency is identified by "country_currency_desc" (e.g. "Australia-Dollar").
/// </summary>
public class TreasuryFxService : IFxService
{
    private const string RatesEndpoint = "/v1/accounting/od/rates_of_exchange";
    private const string Fields = "country_currency_desc,exchange_rate,record_date";

    private readonly HttpClient _httpClient;
    private readonly ILogger<TreasuryFxService> _logger;

    public TreasuryFxService(HttpClient httpClient, ILogger<TreasuryFxService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ExchangeRate?> GetRateOnOrBeforeDateAsync(string currency, DateOnly date)
    {
        var sixMonthCutoff = date.AddMonths(-6);
        var url = BuildUrl(currency, $"record_date:lte:{date:yyyy-MM-dd}");

        _logger.LogInformation("Fetching Treasury FX rate for {Currency} on or before {Date}", currency, date);

        var entry = await FetchFirstEntryAsync(url);
        if (entry == null)
            return null;

        var recordDate = DateOnly.Parse(entry.RecordDate);

        // Req 3: rate must be within 6 months prior to the transaction date
        if (recordDate < sixMonthCutoff)
        {
            _logger.LogWarning(
                "Rate found for {Currency} on {RecordDate} is older than 6 months before {TransactionDate}",
                currency, recordDate, date);
            return null;
        }

        return new ExchangeRate(decimal.Parse(entry.ExchangeRate), recordDate);
    }

    public async Task<ExchangeRate?> GetLatestRateAsync(string currency)
    {
        var url = BuildUrl(currency, filter: null);

        _logger.LogInformation("Fetching latest Treasury FX rate for {Currency}", currency);

        var entry = await FetchFirstEntryAsync(url);
        if (entry == null)
            return null;

        return new ExchangeRate(
            decimal.Parse(entry.ExchangeRate),
            DateOnly.Parse(entry.RecordDate));
    }

    private string BuildUrl(string currency, string? filter)
    {
        var currencyFilter = $"country_currency_desc:eq:{Uri.EscapeDataString(currency)}";
        var combinedFilter = filter != null
            ? $"{currencyFilter},{filter}"
            : currencyFilter;

        return $"{RatesEndpoint}?fields={Fields}&filter={combinedFilter}&sort=-record_date&page[size]=1";
    }

    private async Task<TreasuryRateEntry?> FetchFirstEntryAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TreasuryResponse>(content, JsonOptions);

            return result?.Data?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve exchange rate from Treasury API. URL: {Url}", url);
            throw new InvalidOperationException("Unable to retrieve exchange rate from the Treasury API. Please try again later.", ex);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
