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
    private readonly Mock<ICardRepository> _cardRepoMock = new();
    private readonly Mock<IFxService> _fxServiceMock = new();
    private readonly CardService _sut;

    public CardServiceTests()
    {
        _sut = new CardService(_cardRepoMock.Object, _fxServiceMock.Object, NullLogger<CardService>.Instance);
    }

    // --- Requirement 1 ---

    [Fact]
    public async Task CreateCardAsync_ValidRequest_ReturnsCardWithGeneratedId()
    {
        _cardRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Card>()))
            .ReturnsAsync((Card c) => c);

        var result = await _sut.CreateCardAsync(new CreateCardRequest { CreditLimit = 1000m });

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(1000m, result.CreditLimit);
        _cardRepoMock.Verify(r => r.AddAsync(It.Is<Card>(c => c.CreditLimit == 1000m)), Times.Once);
    }

    // --- Requirement 4 ---

    [Fact]
    public async Task GetBalanceAsync_NoTransactions_ReturnsFullCreditLimit()
    {
        var card = new Card { Id = Guid.NewGuid(), CreditLimit = 5000m, Transactions = new List<Transaction>() };
        _cardRepoMock.Setup(r => r.GetByIdWithTransactionsAsync(card.Id)).ReturnsAsync(card);

        var result = await _sut.GetBalanceAsync(card.Id, "United States-Dollar");

        Assert.Equal(5000m, result.AvailableBalance);
        Assert.Equal(0m, result.TotalTransactions);
        Assert.Equal(1m, result.ExchangeRate);
        _fxServiceMock.Verify(f => f.GetLatestRateAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetBalanceAsync_WithTransactions_DeductsFromCreditLimit()
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            CreditLimit = 1000m,
            Transactions = new List<Transaction>
            {
                new() { Amount = 200m },
                new() { Amount = 150m }
            }
        };
        _cardRepoMock.Setup(r => r.GetByIdWithTransactionsAsync(card.Id)).ReturnsAsync(card);

        var result = await _sut.GetBalanceAsync(card.Id, "USD");

        Assert.Equal(350m, result.TotalTransactions);
        Assert.Equal(650m, result.AvailableBalance);
    }

    [Fact]
    public async Task GetBalanceAsync_WithCurrencyConversion_AppliesExchangeRate()
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            CreditLimit = 1000m,
            Transactions = new List<Transaction> { new() { Amount = 100m } }
        };
        _cardRepoMock.Setup(r => r.GetByIdWithTransactionsAsync(card.Id)).ReturnsAsync(card);
        _fxServiceMock
            .Setup(f => f.GetLatestRateAsync("Australia-Dollar"))
            .ReturnsAsync(new ExchangeRate(1.5m, new DateOnly(2026, 3, 31)));

        var result = await _sut.GetBalanceAsync(card.Id, "Australia-Dollar");

        // Available balance in USD = 900. At rate 1.5, AUD = 1350.
        Assert.Equal(1350m, result.AvailableBalance);
        Assert.Equal(1.5m, result.ExchangeRate);
        Assert.Equal("Australia-Dollar", result.Currency);
    }

    [Fact]
    public async Task GetBalanceAsync_WhenNoRateAvailable_ThrowsInvalidOperationException()
    {
        var card = new Card { Id = Guid.NewGuid(), CreditLimit = 1000m, Transactions = new List<Transaction>() };
        _cardRepoMock.Setup(r => r.GetByIdWithTransactionsAsync(card.Id)).ReturnsAsync(card);
        _fxServiceMock.Setup(f => f.GetLatestRateAsync("Unknown-Currency")).ReturnsAsync((ExchangeRate?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.GetBalanceAsync(card.Id, "Unknown-Currency"));
    }

    [Fact]
    public async Task GetBalanceAsync_WhenCardNotFound_ThrowsKeyNotFoundException()
    {
        _cardRepoMock.Setup(r => r.GetByIdWithTransactionsAsync(It.IsAny<Guid>())).ReturnsAsync((Card?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.GetBalanceAsync(Guid.NewGuid(), "USD"));
    }
}
