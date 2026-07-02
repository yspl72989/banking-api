using BankingApi.Currency;
using BankingApi.Domain;
using BankingApi.Models.Requests;
using BankingApi.Models.Responses;
using BankingApi.Repositories.Contracts;
using BankingApi.Services.Contracts;

namespace BankingApi.Services;

public class CardService : ICardService
{
    private readonly ICardRepository _cardRepository;
    private readonly IFxService _fxService;
    private readonly ILogger<CardService> _logger;

    public CardService(ICardRepository cardRepository, IFxService fxService, ILogger<CardService> logger)
    {
        _cardRepository = cardRepository;
        _fxService = fxService;
        _logger = logger;
    }

    /// <summary>Req 1: Create and persist a card with a credit limit (ISO 4217 currency).</summary>
    public async Task<CardResponse> CreateCardAsync(CreateCardRequest request)
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            CreditLimit = request.CreditLimit,
            CreditLimitCurrency = SupportedCurrencies.Validate(request.CreditLimitCurrency),
            CreatedAt = DateTime.UtcNow
        };

        await _cardRepository.AddAsync(card);
        _logger.LogInformation("Card {CardId} created with limit {Limit} {Currency}",
            card.Id, card.CreditLimit, card.CreditLimitCurrency);

        return MapToResponse(card);
    }

    /// <summary>
    /// Req 4: Available balance = credit limit minus sum of all transactions in the requested currency.
    ///
    /// Uses LATEST Treasury FX rates (current exposure valuation — see README).
    /// Each item converted individually before subtraction (financially correct for mixed-currency cards).
    /// Rate cache by (from, to) pair avoids duplicate API calls within one request.
    /// </summary>
    public async Task<BalanceResponse> GetBalanceAsync(Guid cardId, string currency)
    {
        var card = await _cardRepository.GetByIdWithTransactionsAsync(cardId)
            ?? throw new KeyNotFoundException($"Card {cardId} not found.");

        currency = SupportedCurrencies.Validate(currency);

        var rateCache = new Dictionary<(string from, string to), decimal>(
            EqualityComparer<(string, string)>.Default);

        async Task<decimal> GetCachedRate(string from, string to)
        {
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return 1m;
            var key = (from.ToUpperInvariant(), to.ToUpperInvariant());
            if (rateCache.TryGetValue(key, out var cached)) return cached;

            var entry = await _fxService.GetLatestRateAsync(from, to)
                ?? throw new InvalidOperationException(
                    $"No exchange rate available for '{from}' to '{to}'. Cannot calculate balance.");

            rateCache[key] = entry.Rate;
            return entry.Rate;
        }

        var limitRate = await GetCachedRate(card.CreditLimitCurrency, currency);
        var creditLimitConverted = Math.Round(card.CreditLimit * limitRate, 2);

        var totalTransactionsConverted = 0m;
        foreach (var tx in card.Transactions)
        {
            var txRate = await GetCachedRate(tx.CurrencyCode, currency);
            totalTransactionsConverted += tx.Amount * txRate;
        }
        totalTransactionsConverted = Math.Round(totalTransactionsConverted, 2);

        _logger.LogInformation(
            "Balance for card {CardId} in {Currency}: limit={Limit}, spend={Spend}",
            cardId, currency, creditLimitConverted, totalTransactionsConverted);

        return new BalanceResponse
        {
            CardId = card.Id,
            Currency = currency,
            CreditLimit = creditLimitConverted,
            TotalTransactions = totalTransactionsConverted,
            AvailableBalance = Math.Round(creditLimitConverted - totalTransactionsConverted, 2)
        };
    }

    private static CardResponse MapToResponse(Card card) => new()
    {
        Id = card.Id,
        CreditLimit = card.CreditLimit,
        CreditLimitCurrency = card.CreditLimitCurrency,
        CreatedAt = card.CreatedAt
    };
}
