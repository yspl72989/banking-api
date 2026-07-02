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
    private readonly Mock<ITransactionRepository> _txRepo = new();
    private readonly Mock<ICardRepository> _cardRepo = new();
    private readonly Mock<IFxService> _fxService = new();
    private readonly TransactionService _sut;

    public TransactionServiceTests()
    {
        _sut = new TransactionService(_txRepo.Object, _cardRepo.Object, _fxService.Object, NullLogger<TransactionService>.Instance);
    }

    [Fact]
    public async Task CreateTransactionAsync_StoresTransactionWithIsoCode()
    {
        var card = new Card { Id = Guid.NewGuid() };
        _cardRepo.Setup(r => r.GetByIdAsync(card.Id)).ReturnsAsync(card);
        _txRepo.Setup(r => r.AddAsync(It.IsAny<Transaction>())).ReturnsAsync((Transaction t) => t);

        var result = await _sut.CreateTransactionAsync(card.Id, new CreateTransactionRequest
        { Description = "Coffee", TransactionDate = new DateOnly(2026, 6, 1), Amount = 4.50m, CurrencyCode = "AUD" });

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("AUD", result.CurrencyCode);
    }

    [Fact]
    public async Task CreateTransactionAsync_CardNotFound_ThrowsKeyNotFoundException()
    {
        _cardRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Card?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.CreateTransactionAsync(Guid.NewGuid(), new CreateTransactionRequest
            { Description = "T", TransactionDate = DateOnly.FromDateTime(DateTime.Today), Amount = 10m, CurrencyCode = "USD" }));
    }

    [Fact]
    public async Task GetConvertedTransactionAsync_SameCurrency_ReturnsRateOneWithNoFxCall()
    {
        var tx = new Transaction { Id = Guid.NewGuid(), CurrencyCode = "AUD",
            Amount = 100m, TransactionDate = new DateOnly(2026, 5, 1), Description = "Test" };
        _txRepo.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);

        var result = await _sut.GetConvertedTransactionAsync(tx.Id, "AUD");

        Assert.Equal(1m, result.ExchangeRate);
        Assert.Equal(100m, result.ConvertedAmount);
        _fxService.Verify(f => f.GetHistoricalRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task GetConvertedTransactionAsync_UsdToAud_SingleFxCall()
    {
        // FX service returns direct USD→AUD rate — no pivot math in service
        var txDate = new DateOnly(2026, 5, 15);
        var tx = new Transaction { Id = Guid.NewGuid(), CurrencyCode = "USD",
            Amount = 100m, TransactionDate = txDate, Description = "Amazon" };
        _txRepo.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);
        _fxService.Setup(f => f.GetHistoricalRateAsync("USD", "AUD", txDate))
            .ReturnsAsync(new ExchangeRate("USD", "AUD", 1.494m, new DateOnly(2026, 3, 31)));

        var result = await _sut.GetConvertedTransactionAsync(tx.Id, "AUD");

        Assert.Equal(1.494m, result.ExchangeRate);
        Assert.Equal(Math.Round(100m * 1.494m, 2), result.ConvertedAmount);
        Assert.Equal("USD", result.OriginalCurrency);
        _fxService.Verify(f => f.GetHistoricalRateAsync("USD", "AUD", txDate), Times.Once);
    }

    [Fact]
    public async Task GetConvertedTransactionAsync_EurToAud_SingleFxCall()
    {
        // USD pivot is inside TreasuryFxService — TransactionService receives a clean direct rate
        var txDate = new DateOnly(2026, 5, 15);
        var directRate = 1.619m;
        var tx = new Transaction { Id = Guid.NewGuid(), CurrencyCode = "EUR",
            Amount = 85m, TransactionDate = txDate, Description = "European purchase" };
        _txRepo.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);
        _fxService.Setup(f => f.GetHistoricalRateAsync("EUR", "AUD", txDate))
            .ReturnsAsync(new ExchangeRate("EUR", "AUD", directRate, new DateOnly(2026, 3, 31)));

        var result = await _sut.GetConvertedTransactionAsync(tx.Id, "AUD");

        Assert.Equal(directRate, result.ExchangeRate);
        Assert.Equal(Math.Round(85m * directRate, 2), result.ConvertedAmount);
        _fxService.Verify(f => f.GetHistoricalRateAsync("EUR", "AUD", txDate), Times.Once);
    }

    [Fact]
    public async Task GetConvertedTransactionAsync_AudToUsd_SingleFxCall()
    {
        var txDate = new DateOnly(2026, 4, 10);
        var directRate = 0.669m;
        var tx = new Transaction { Id = Guid.NewGuid(), CurrencyCode = "AUD",
            Amount = 200m, TransactionDate = txDate, Description = "Flight" };
        _txRepo.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);
        _fxService.Setup(f => f.GetHistoricalRateAsync("AUD", "USD", txDate))
            .ReturnsAsync(new ExchangeRate("AUD", "USD", directRate, new DateOnly(2026, 3, 31)));

        var result = await _sut.GetConvertedTransactionAsync(tx.Id, "USD");

        Assert.Equal(Math.Round(200m * directRate, 2), result.ConvertedAmount);
    }

    [Fact]
    public async Task GetConvertedTransactionAsync_NoRateWithinSixMonths_ThrowsInvalidOperationException()
    {
        var tx = new Transaction { Id = Guid.NewGuid(), CurrencyCode = "USD",
            Amount = 50m, TransactionDate = new DateOnly(2026, 1, 1) };
        _txRepo.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);
        _fxService.Setup(f => f.GetHistoricalRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>()))
            .ReturnsAsync((ExchangeRate?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.GetConvertedTransactionAsync(tx.Id, "AUD"));
    }

    [Fact]
    public async Task GetConvertedTransactionAsync_TransactionNotFound_ThrowsKeyNotFoundException()
    {
        _txRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Transaction?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.GetConvertedTransactionAsync(Guid.NewGuid(), "AUD"));
    }
}
