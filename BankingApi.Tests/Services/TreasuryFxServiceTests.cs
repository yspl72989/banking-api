using BankingApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace BankingApi.Tests.Services;

public class TreasuryFxServiceTests
{
    [Fact]
    public async Task GetLatestRateAsync_HkdToUsd_OnlyCallsTreasuryOnceAndInvertsRate()
    {
        var handler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":[{"country_currency_desc":"Hong Kong-Dollar","exchange_rate":"7.850","record_date":"2026-03-31"}]}""")
            });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.fiscaldata.treasury.gov/services/api/fiscal_service/")
        };

        var sut = new TreasuryFxService(client, NullLogger<TreasuryFxService>.Instance);
        var rate = await sut.GetLatestRateAsync("HKD", "USD");

        Assert.NotNull(rate);
        Assert.Equal(1m / 7.850m, rate!.Rate);
        Assert.Equal("HKD", rate.FromCurrency);
        Assert.Equal("USD", rate.ToCurrency);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains("Hong%20Kong-Dollar", handler.LastRequestUri!);
        Assert.Contains("/services/api/fiscal_service/", handler.LastRequestUri);
    }

    [Fact]
    public async Task GetLatestRateAsync_UsdToHkd_OnlyCallsTreasuryForHkd()
    {
        var handler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":[{"country_currency_desc":"Hong Kong-Dollar","exchange_rate":"7.850","record_date":"2026-03-31"}]}""")
            });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.fiscaldata.treasury.gov/services/api/fiscal_service/")
        };

        var sut = new TreasuryFxService(client, NullLogger<TreasuryFxService>.Instance);
        var rate = await sut.GetLatestRateAsync("USD", "HKD");

        Assert.NotNull(rate);
        Assert.Equal(7.850m, rate!.Rate);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetLatestRateAsync_UsdToUsd_ReturnsOneWithoutCallingTreasury()
    {
        var handler = new CountingHandler(_ => throw new InvalidOperationException("Should not call Treasury"));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.fiscaldata.treasury.gov/services/api/fiscal_service/")
        };

        var sut = new TreasuryFxService(client, NullLogger<TreasuryFxService>.Instance);
        var rate = await sut.GetLatestRateAsync("USD", "USD");

        Assert.NotNull(rate);
        Assert.Equal(1m, rate!.Rate);
        Assert.Equal(0, handler.CallCount);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        public int CallCount { get; private set; }
        public string? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri?.AbsoluteUri;
            return Task.FromResult(_responder(request));
        }
    }
}
