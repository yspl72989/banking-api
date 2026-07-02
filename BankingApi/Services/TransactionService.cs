using BankingApi.Models.Entities;
using BankingApi.Models.Requests;
using BankingApi.Services.Contracts;
using System.Collections.Concurrent;

namespace BankingApi.Services;

/// <summary>
/// Processes purchase transactions against credit cards.
/// Handles multi-currency transactions via the FX service.
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly ConcurrentDictionary<Guid, List<Transaction>> _transactionsByCard = new();
    private readonly ICreditCardService _creditCardService;
    private readonly IFxService _fxService;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ICreditCardService creditCardService,
        IFxService fxService,
        ILogger<TransactionService> logger)
    {
        _creditCardService = creditCardService;
        _fxService = fxService;
        _logger = logger;
    }

    public async Task<Transaction> ProcessPurchaseAsync(Guid cardId, ProcessTransactionRequest request)
    {
        var card = await _creditCardService.GetCardAsync(cardId);

        if (card == null)
            throw new KeyNotFoundException($"Card {cardId} not found.");

        if (!card.IsActive)
            return DeclinedTransaction(cardId, request, "Card is inactive.");

        // Get the exchange rate if the transaction currency differs from the card's currency
        var exchangeRate = await _fxService.GetExchangeRateAsync(request.Currency, card.Currency);
        var amountInCardCurrency = Math.Round(request.Amount * exchangeRate, 2);

        _logger.LogInformation(
            "Processing {Amount} {TxCurrency} purchase on card {CardId}. Converted: {Converted} {CardCurrency} at rate {Rate}",
            request.Amount, request.Currency, cardId, amountInCardCurrency, card.Currency, exchangeRate);

        var wasDebited = await _creditCardService.DebitAsync(cardId, amountInCardCurrency);

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            ExchangeRate = exchangeRate,
            AmountInCardCurrency = amountInCardCurrency,
            Description = request.Description,
            Type = TransactionType.Purchase,
            Status = wasDebited ? TransactionStatus.Approved : TransactionStatus.Declined,
            DeclineReason = wasDebited ? null : "Insufficient available credit.",
            ProcessedAt = DateTime.UtcNow
        };

        RecordTransaction(cardId, transaction);
        return transaction;
    }

    public IEnumerable<Transaction> GetTransactionsForCard(Guid cardId)
    {
        if (_transactionsByCard.TryGetValue(cardId, out var transactions))
            return transactions.OrderByDescending(t => t.ProcessedAt);

        return Enumerable.Empty<Transaction>();
    }

    private Transaction DeclinedTransaction(Guid cardId, ProcessTransactionRequest request, string reason)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            ExchangeRate = 1m,
            AmountInCardCurrency = request.Amount,
            Description = request.Description,
            Type = TransactionType.Purchase,
            Status = TransactionStatus.Declined,
            DeclineReason = reason,
            ProcessedAt = DateTime.UtcNow
        };

        RecordTransaction(cardId, transaction);
        return transaction;
    }

    private void RecordTransaction(Guid cardId, Transaction transaction)
    {
        _transactionsByCard.AddOrUpdate(
            cardId,
            _ => [transaction],
            (_, existing) => { existing.Add(transaction); return existing; });
    }
}
