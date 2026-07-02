using BankingApi.Domain;
using BankingApi.Models.Requests;
using BankingApi.Repositories.Contracts;
using BankingApi.Services;
using BankingApi.Services.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BankingApi.Tests.Services;

public class TransactionServiceTests
{
    private readonly Mock<ITransactionRepository> _txRepoMock = new();
    private readonly Mock<ICardRepository> _cardRepoMock = new();
    private readonly Mock<IFxService> _fxServiceMock = new();
    private readonly TransactionService _sut;

    public TransactionServiceTests()
    {
        _sut = new TransactionService(
            _txRepoMock.Object,
            _cardRepoMock.Object,
            _fxServiceMock.Object,
            NullLogger<TransactionService>.Instance);
    }

    // --- Requirement 2 ---

    [Fact]
    public async Task CreateTransactionAsync_ValidRequest_ReturnsTransactionWithGeneratedId()
    {
        var card = new Card { Id = Guid.NewGuid(), CreditLimit = 5000m };
        _cardRepoMock.Setup(r => r.GetByIdAsync(card.Id)).ReturnsAsync(card);
        _txRepoMock.Setup(r => r.AddAsync(It.IsAny<Transaction>())).ReturnsAsync((Transaction t) => t);

        var request = new CreateTransactionRequest
        {
            Description = "Coffee",
            TransactionDate = new DateOnly(2026, 6, 1),
            Amount = 4.50m
        };

        var result = await _sut.CreateTransactionAsync(card.Id, request);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Coffee", result.Description);
        Assert.Equal(4.50m, result.Amount);
        Assert.Equal(card.Id, result.CardId);
    }

    [Fact]
    public async Task CreateTransactionAsync_CardNotFound_ThrowsKeyNotFoundException()
    {
        _cardRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Card?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.CreateTransactionAsync(Guid.NewGuid(), new CreateTransactionRequest
            {
                Description = "Test",
                TransactionDate = DateOnly.FromDateTime(DateTime.Today),
                Amount = 10m
            }));
    }

    // --- Requirement 3 ---

    [Fact]
    public async Task GetConvertedTransactionAsync_ValidRequest_ReturnsConvertedAmount()
    {
        var txDate = new DateOnly(2026, 5, 15);
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            Description = "Amazon purchase",
            TransactionDate = txDate,
            Amount = 100m
        };
        _txRepoMock.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);
        _fxServiceMock
            .Setup(f => f.GetRateOnOrBeforeDateAsync("Australia-Dollar", txDate))
            .ReturnsAsync(new ExchangeRate(1.494m, new DateOnly(2026, 3, 31)));

        var result = await _sut.GetConvertedTransactionAsync(tx.Id, "Australia-Dollar");

        Assert.Equal(tx.Id, result.Id);
        Assert.Equal(100m, result.OriginalAmount);
        Assert.Equal(1.494m, result.ExchangeRate);
        Assert.Equal(149.40m, result.ConvertedAmount);
        Assert.Equal("Australia-Dollar", result.Currency);
    }

    [Fact]
    public async Task GetConvertedTransactionAsync_NoRateWithinSixMonths_ThrowsInvalidOperationException()
    {
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = new DateOnly(2026, 1, 1),
            Amount = 50m
        };
        _txRepoMock.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);
        _fxServiceMock
            .Setup(f => f.GetRateOnOrBeforeDateAsync(It.IsAny<string>(), It.IsAny<DateOnly>()))
            .ReturnsAsync((ExchangeRate?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.GetConvertedTransactionAsync(tx.Id, "Some-Currency"));
    }

    [Fact]
    public async Task GetConvertedTransactionAsync_TransactionNotFound_ThrowsKeyNotFoundException()
    {
        _txRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Transaction?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.GetConvertedTransactionAsync(Guid.NewGuid(), "Australia-Dollar"));
    }

    [Fact]
    public async Task GetConvertedTransactionAsync_RoundsTwoDecimalPlaces()
    {
        var txDate = new DateOnly(2026, 6, 1);
        var tx = new Transaction { Id = Guid.NewGuid(), TransactionDate = txDate, Amount = 10m };
        _txRepoMock.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);
        _fxServiceMock
            .Setup(f => f.GetRateOnOrBeforeDateAsync("Euro Zone-Euro", txDate))
            .ReturnsAsync(new ExchangeRate(0.923m, new DateOnly(2026, 3, 31)));

        var result = await _sut.GetConvertedTransactionAsync(tx.Id, "Euro Zone-Euro");

        Assert.Equal(Math.Round(10m * 0.923m, 2), result.ConvertedAmount);
    }
}
