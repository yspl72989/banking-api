using BankingApi.Data;
using BankingApi.Models.Requests;
using BankingApi.Models.Responses;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace BankingApi.Tests.Integration;

public class CardsIntegrationTests : IClassFixture<BankingApiFactory>
{
    private readonly HttpClient _client;

    public CardsIntegrationTests(BankingApiFactory factory)
    {
        _client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<BankingDbContext>().Database.EnsureCreated();
    }

    [Fact]
    public async Task PostCard_ValidRequest_Returns201WithIsoCode()
    {
        var response = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest { CreditLimit = 5000m, CreditLimitCurrency = "AUD" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.NotNull(card);
        Assert.NotEqual(Guid.Empty, card.Id);
        Assert.Equal("AUD", card.CreditLimitCurrency);
    }

    [Fact]
    public async Task PostCard_ZeroCreditLimit_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/cards", new CreateCardRequest { CreditLimit = 0m });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTransaction_ValidRequest_Returns201WithIsoCode()
    {
        var cardResp = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest { CreditLimit = 2000m, CreditLimitCurrency = "USD" });
        var card = await cardResp.Content.ReadFromJsonAsync<CardResponse>();

        var response = await _client.PostAsJsonAsync($"/api/cards/{card!.Id}/transactions",
            new CreateTransactionRequest
            { Description = "Integration test", TransactionDate = new DateOnly(2026, 6, 15), Amount = 250m, CurrencyCode = "USD" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var tx = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(tx);
        Assert.Equal("USD", tx.CurrencyCode);
    }

    [Fact]
    public async Task PostTransaction_CardNotFound_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/cards/{Guid.NewGuid()}/transactions",
            new CreateTransactionRequest
            { Description = "Orphan", TransactionDate = DateOnly.FromDateTime(DateTime.Today), Amount = 10m, CurrencyCode = "USD" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBalance_SameCurrency_ReturnsCorrectBalance()
    {
        var cardResp = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest { CreditLimit = 1000m, CreditLimitCurrency = "USD" });
        var card = await cardResp.Content.ReadFromJsonAsync<CardResponse>();

        await _client.PostAsJsonAsync($"/api/cards/{card!.Id}/transactions",
            new CreateTransactionRequest { Description = "T1", TransactionDate = DateOnly.FromDateTime(DateTime.Today), Amount = 300m, CurrencyCode = "USD" });

        var response = await _client.GetAsync($"/api/cards/{card.Id}/balance?currency=USD");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        Assert.NotNull(balance);
        Assert.Equal(700m, balance.AvailableBalance);
        Assert.Equal("USD", balance.Currency);
    }

    [Fact]
    public async Task GetBalance_CardNotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/cards/{Guid.NewGuid()}/balance?currency=USD");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTransaction_NotFound_Returns404()
    {
        var cardResp = await _client.PostAsJsonAsync("/api/cards", new CreateCardRequest { CreditLimit = 1000m });
        var card = await cardResp.Content.ReadFromJsonAsync<CardResponse>();
        var response = await _client.GetAsync($"/api/cards/{card!.Id}/transactions/{Guid.NewGuid()}?currency=USD");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
