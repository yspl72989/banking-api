using BankingApi.Services.Contracts;
using System.Text.Json;

namespace BankingApi.Services;

/// <summary>
/// Integrates with the Frankfurter public Treasury FX API (https://api.frankfurter.app)
/// to retrieve live exchange rates between currency pairs.
/// Assumption: Frankfurter is used as the Treasury FX provider since no internal endpoint was specified.
/// </summary>
public class FxService : IFxService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FxService> _logger;

    public FxService(HttpClient httpClient, ILogger<FxService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency)
    {
        var from = fromCurrency.ToUpperInvariant();
        var to = toCurrency.ToUpperInvariant();

        if (from == to)
            return 1m;

        try
        {
            var url = $"https://api.frankfurter.app/latest?from={from}&to={to}";
            _logger.LogInformation("Fetching FX rate from Treasury API: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            // Response shape: { "rates": { "AUD": 1.5432 } }
            var rate = doc.RootElement
                .GetProperty("rates")
                .GetProperty(to)
                .GetDecimal();

            _logger.LogInformation("FX rate {From} -> {To} = {Rate}", from, to, rate);
            return rate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve FX rate from Treasury API for {From} -> {To}", fromCurrency, toCurrency);
            throw new InvalidOperationException($"Unable to retrieve exchange rate for {fromCurrency} to {toCurrency}. Please try again later.", ex);
        }
    }
}
