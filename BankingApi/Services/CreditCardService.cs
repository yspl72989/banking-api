using BankingApi.Models.Entities;
using BankingApi.Models.Requests;
using BankingApi.Services.Contracts;
using System.Collections.Concurrent;

namespace BankingApi.Services;

/// <summary>
/// Manages credit card creation and available credit tracking.
/// Uses in-memory storage — assumption: persistence layer is out of scope for this exercise.
/// </summary>
public class CreditCardService : ICreditCardService
{
    private readonly ConcurrentDictionary<Guid, CreditCard> _cards = new();
    private readonly ILogger<CreditCardService> _logger;
    private readonly Random _random = new();

    public CreditCardService(ILogger<CreditCardService> logger)
    {
        _logger = logger;
    }

    public Task<CreditCard> CreateCardAsync(CreateCardRequest request)
    {
        var card = new CreditCard
        {
            Id = Guid.NewGuid(),
            CardNumber = GenerateCardNumber(),
            CardHolderName = request.CardHolderName,
            ExpiryDate = GenerateExpiryDate(),
            Cvv = GenerateCvv(),
            CreditLimit = request.CreditLimit,
            AvailableCredit = request.CreditLimit,
            Currency = request.Currency.ToUpperInvariant(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _cards[card.Id] = card;
        _logger.LogInformation("Credit card created for {CardHolder} with limit {Limit} {Currency}", 
            card.CardHolderName, card.CreditLimit, card.Currency);

        return Task.FromResult(card);
    }

    public Task<CreditCard?> GetCardAsync(Guid cardId)
    {
        _cards.TryGetValue(cardId, out var card);
        return Task.FromResult(card);
    }

    public IEnumerable<CreditCard> GetAllCards() => _cards.Values;

    public Task<bool> DebitAsync(Guid cardId, decimal amountInCardCurrency)
    {
        if (!_cards.TryGetValue(cardId, out var card))
            return Task.FromResult(false);

        if (!card.IsActive || card.AvailableCredit < amountInCardCurrency)
            return Task.FromResult(false);

        card.AvailableCredit -= amountInCardCurrency;
        _logger.LogInformation("Debited {Amount} from card {CardId}. Available credit: {Available}", 
            amountInCardCurrency, cardId, card.AvailableCredit);

        return Task.FromResult(true);
    }

    public Task CreditAsync(Guid cardId, decimal amountInCardCurrency)
    {
        if (!_cards.TryGetValue(cardId, out var card))
            return Task.CompletedTask;

        card.AvailableCredit = Math.Min(card.CreditLimit, card.AvailableCredit + amountInCardCurrency);
        _logger.LogInformation("Credited {Amount} to card {CardId}. Available credit: {Available}", 
            amountInCardCurrency, cardId, card.AvailableCredit);

        return Task.CompletedTask;
    }

    private string GenerateCardNumber()
    {
        // Generates a 16-digit number in the format 4XXX-XXXX-XXXX-XXXX (Visa-style prefix)
        return $"4{_random.Next(100, 999)}{_random.Next(1000, 9999)}{_random.Next(1000, 9999)}{_random.Next(1000, 9999)}";
    }

    private static string GenerateExpiryDate()
    {
        var expiry = DateTime.UtcNow.AddYears(3);
        return expiry.ToString("MM/yy");
    }

    private string GenerateCvv()
    {
        return _random.Next(100, 999).ToString();
    }
}
