using BankingApi.Domain;
using BankingApi.Models.Requests;
using BankingApi.Models.Responses;
using BankingApi.Repositories.Contracts;
using BankingApi.Services.Contracts;

namespace BankingApi.Services;

/// <summary>
/// Handles Requirement 1 (create card) and Requirement 4 (available balance).
/// Facade over ICardRepository and IFxService.
/// </summary>
public class CardService : ICardService
{
    // The Treasury API expresses all rates as "X foreign units per 1 USD".
    // Transactions and credit limits are therefore stored in USD.
    private const string BaseCurrency = "United States-Dollar";

    private readonly ICardRepository _cardRepository;
    private readonly IFxService _fxService;
    private readonly ILogger<CardService> _logger;

    public CardService(ICardRepository cardRepository, IFxService fxService, ILogger<CardService> logger)
    {
        _cardRepository = cardRepository;
        _fxService = fxService;
        _logger = logger;
    }

    /// <summary>Req 1: Create and persist a card with a credit limit.</summary>
    public async Task<CardResponse> CreateCardAsync(CreateCardRequest request)
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            CreditLimit = request.CreditLimit,
            CreatedAt = DateTime.UtcNow
        };

        await _cardRepository.AddAsync(card);
        _logger.LogInformation("Card {CardId} created with credit limit {CreditLimit} USD", card.Id, card.CreditLimit);

        return MapToResponse(card);
    }

    /// <summary>
    /// Req 4: Available balance = credit limit minus sum of all transactions.
    /// Converts to the requested currency using the latest Treasury FX rate.
    /// </summary>
    public async Task<BalanceResponse> GetBalanceAsync(Guid cardId, string currency)
    {
        var card = await _cardRepository.GetByIdWithTransactionsAsync(cardId)
            ?? throw new KeyNotFoundException($"Card {cardId} not found.");

        var totalTransactions = card.Transactions.Sum(t => t.Amount);
        var availableBalanceUsd = card.CreditLimit - totalTransactions;

        // If requesting USD (base currency), no FX conversion needed
        if (IsBaseCurrency(currency))
        {
            return new BalanceResponse
            {
                CardId = card.Id,
                CreditLimit = card.CreditLimit,
                TotalTransactions = totalTransactions,
                AvailableBalance = availableBalanceUsd,
                Currency = BaseCurrency,
                ExchangeRate = 1m,
                ExchangeRateDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };
        }

        var rate = await _fxService.GetLatestRateAsync(currency)
            ?? throw new InvalidOperationException(
                $"No exchange rate is available for '{currency}'. Cannot calculate balance in the requested currency.");

        return new BalanceResponse
        {
            CardId = card.Id,
            CreditLimit = Math.Round(card.CreditLimit * rate.Rate, 2),
            TotalTransactions = Math.Round(totalTransactions * rate.Rate, 2),
            AvailableBalance = Math.Round(availableBalanceUsd * rate.Rate, 2),
            Currency = currency,
            ExchangeRate = rate.Rate,
            ExchangeRateDate = rate.RecordDate
        };
    }

    private static bool IsBaseCurrency(string currency) =>
        currency.Equals(BaseCurrency, StringComparison.OrdinalIgnoreCase) ||
        currency.Equals("USD", StringComparison.OrdinalIgnoreCase);

    private static CardResponse MapToResponse(Card card) => new()
    {
        Id = card.Id,
        CreditLimit = card.CreditLimit,
        CreatedAt = card.CreatedAt
    };
}
