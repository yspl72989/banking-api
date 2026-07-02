using BankingApi.Domain;
using BankingApi.Models.Requests;
using BankingApi.Repositories.Contracts;
using BankingApi.Services;
using BankingApi.Services.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BankingApi.Tests.Services;

public class CardServiceTests
{
    private readonly Mock<ICardRepository> _cardRepo = new();
    private readonly Mock<IFxService> _fxService = new();
    private readonly CardService _sut;

    public CardServiceTests()
    {
        _sut = new CardService(_cardRepo.Object, _fxService.Object, NullLogger<CardService>.Instance);
    }

    [Fact]
    public async Task CreateCardAsync_StoresCardWithIsoCodeAndGeneratedId()
    {
        _cardRepo.Setup(r => r.AddAsync(It.IsAny<Card>())).ReturnsAsync((Card c) => c);
        var result = await _sut.CreateCardAsync(new CreateCardRequest { CreditLimit = 5000m, CreditLimitCurrency = "AUD" });
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(5000m, result.CreditLimit);
        Assert.Equal("AUD", result.CreditLimitCurrency);
    }

    [Fact]
    public async Task GetBalanceAsync_NoTransactions_ConvertsCreditLimitDirectly()
    {
        // Card 1000 AUD, requested in EUR — FX service returns direct AUD→EUR rate 0.618
        var card = new Card { Id = Guid.NewGuid(), CreditLimit = 1000m, CreditLimitCurrency = "AUD", Transactions = new List<Transaction>() };
        _cardRepo.Setup(r => r.GetByIdWithTransactionsAsync(card.Id)).ReturnsAsync(card);
        _fxService.Setup(f => f.GetLatestRateAsync("AUD", "EUR"))
            .ReturnsAsync(new ExchangeRate("AUD", "EUR", 0.618m, new DateOnly(2026, 3, 31)));

        var result = await _sut.GetBalanceAsync(card.Id, "EUR");

        Assert.Equal(Math.Round(1000m * 0.618m, 2), result.CreditLimit);
        Assert.Equal(0m, result.TotalTransactions);
        Assert.Equal("EUR", result.Currency);
        _fxService.Verify(f => f.GetLatestRateAsync("AUD", "EUR"), Times.Once);
    }

    [Fact]
    public async Task GetBalanceAsync_ConvertsEachTransactionIndividually()
    {
        // Card 1000 USD, tx1 = 85 EUR, tx2 = 100 USD, requested in AUD
        // USD→AUD = 1.494, EUR→AUD = 1.619
        var card = new Card
        {
            Id = Guid.NewGuid(), CreditLimit = 1000m, CreditLimitCurrency = "USD",
            Transactions = new List<Transaction>
            {
                new() { Amount = 85m,  CurrencyCode = "EUR" },
                new() { Amount = 100m, CurrencyCode = "USD" }
            }
        };
        _cardRepo.Setup(r => r.GetByIdWithTransactionsAsync(card.Id)).ReturnsAsync(card);
        _fxService.Setup(f => f.GetLatestRateAsync("USD", "AUD"))
            .ReturnsAsync(new ExchangeRate("USD", "AUD", 1.494m, new DateOnly(2026, 3, 31)));
        _fxService.Setup(f => f.GetLatestRateAsync("EUR", "AUD"))
            .ReturnsAsync(new ExchangeRate("EUR", "AUD", 1.619m, new DateOnly(2026, 3, 31)));

        var result = await _sut.GetBalanceAsync(card.Id, "AUD");

        var expectedLimit = Math.Round(1000m * 1.494m, 2);
        var expectedTotal = Math.Round(85m * 1.619m + 100m * 1.494m, 2);
        Assert.Equal(expectedLimit, result.CreditLimit);
        Assert.Equal(expectedTotal, result.TotalTransactions);
        Assert.Equal(Math.Round(expectedLimit - expectedTotal, 2), result.AvailableBalance);
        // USD→AUD called once (credit limit), then cache hit for tx2 — only 2 API calls total
        _fxService.Verify(f => f.GetLatestRateAsync("USD", "AUD"), Times.Once);
        _fxService.Verify(f => f.GetLatestRateAsync("EUR", "AUD"), Times.Once);
    }

    [Fact]
    public async Task GetBalanceAsync_SameCurrencyEverywhere_NeverCallsFxService()
    {
        var card = new Card { Id = Guid.NewGuid(), CreditLimit = 500m, CreditLimitCurrency = "USD",
            Transactions = new List<Transaction> { new() { Amount = 100m, CurrencyCode = "USD" } } };
        _cardRepo.Setup(r => r.GetByIdWithTransactionsAsync(card.Id)).ReturnsAsync(card);

        var result = await _sut.GetBalanceAsync(card.Id, "USD");

        Assert.Equal(400m, result.AvailableBalance);
        _fxService.Verify(f => f.GetLatestRateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetBalanceAsync_NoRateAvailable_ThrowsInvalidOperationException()
    {
        var card = new Card { Id = Guid.NewGuid(), CreditLimit = 1000m, CreditLimitCurrency = "USD", Transactions = new List<Transaction>() };
        _cardRepo.Setup(r => r.GetByIdWithTransactionsAsync(card.Id)).ReturnsAsync(card);
        _fxService.Setup(f => f.GetLatestRateAsync("USD", "AUD")).ReturnsAsync((ExchangeRate?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GetBalanceAsync(card.Id, "AUD"));
    }

    [Fact]
    public async Task GetBalanceAsync_UnsupportedCurrency_ThrowsArgumentException()
    {
        var card = new Card { Id = Guid.NewGuid(), CreditLimit = 1000m, CreditLimitCurrency = "USD", Transactions = new List<Transaction>() };
        _cardRepo.Setup(r => r.GetByIdWithTransactionsAsync(card.Id)).ReturnsAsync(card);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetBalanceAsync(card.Id, "ABC"));
    }

    [Fact]
    public async Task GetBalanceAsync_CardNotFound_ThrowsKeyNotFoundException()
    {
        _cardRepo.Setup(r => r.GetByIdWithTransactionsAsync(It.IsAny<Guid>())).ReturnsAsync((Card?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetBalanceAsync(Guid.NewGuid(), "USD"));
    }
}
