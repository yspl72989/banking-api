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

        // Ensure the SQLite schema exists before each test class runs
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task PostCard_ValidRequest_Returns201WithId()
    {
        var response = await _client.PostAsJsonAsync("/api/cards", new CreateCardRequest { CreditLimit = 5000m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.NotNull(card);
        Assert.NotEqual(Guid.Empty, card.Id);
        Assert.Equal(5000m, card.CreditLimit);
    }

    [Fact]
    public async Task PostCard_ZeroCreditLimit_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/cards", new CreateCardRequest { CreditLimit = 0m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTransaction_ValidCardAndRequest_Returns201()
    {
        // Create a card first
        var cardResp = await _client.PostAsJsonAsync("/api/cards", new CreateCardRequest { CreditLimit = 2000m });
        var card = await cardResp.Content.ReadFromJsonAsync<CardResponse>();

        var txRequest = new CreateTransactionRequest
        {
            Description = "Integration test purchase",
            TransactionDate = new DateOnly(2026, 6, 15),
            Amount = 250m
        };

        var response = await _client.PostAsJsonAsync($"/api/cards/{card!.Id}/transactions", txRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var tx = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(tx);
        Assert.Equal("Integration test purchase", tx.Description);
        Assert.Equal(250m, tx.Amount);
    }

    [Fact]
    public async Task PostTransaction_CardNotFound_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/cards/{Guid.NewGuid()}/transactions",
            new CreateTransactionRequest
            {
                Description = "Orphan transaction",
                TransactionDate = DateOnly.FromDateTime(DateTime.Today),
                Amount = 10m
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBalance_UsdBaseCase_ReturnsCorrectAvailableBalance()
    {
        // Create card with $1000 limit
        var cardResp = await _client.PostAsJsonAsync("/api/cards", new CreateCardRequest { CreditLimit = 1000m });
        var card = await cardResp.Content.ReadFromJsonAsync<CardResponse>();

        // Add two transactions totalling $300
        await _client.PostAsJsonAsync($"/api/cards/{card!.Id}/transactions",
            new CreateTransactionRequest { Description = "T1", TransactionDate = DateOnly.FromDateTime(DateTime.Today), Amount = 100m });
        await _client.PostAsJsonAsync($"/api/cards/{card.Id}/transactions",
            new CreateTransactionRequest { Description = "T2", TransactionDate = DateOnly.FromDateTime(DateTime.Today), Amount = 200m });

        var response = await _client.GetAsync($"/api/cards/{card.Id}/balance?currency=USD");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        Assert.NotNull(balance);
        Assert.Equal(700m, balance.AvailableBalance);
        Assert.Equal(300m, balance.TotalTransactions);
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
