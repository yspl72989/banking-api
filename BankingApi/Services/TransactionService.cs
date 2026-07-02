using BankingApi.Domain;
using BankingApi.Models.Requests;
using BankingApi.Models.Responses;
using BankingApi.Repositories.Contracts;
using BankingApi.Services.Contracts;

namespace BankingApi.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICardRepository _cardRepository;
    private readonly IFxService _fxService;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository transactionRepository,
        ICardRepository cardRepository,
        IFxService fxService,
        ILogger<TransactionService> logger)
    {
        _transactionRepository = transactionRepository;
        _cardRepository = cardRepository;
        _fxService = fxService;
        _logger = logger;
    }

    /// <summary>Req 2: Accept and store a purchase transaction. CurrencyCode in ISO 4217.</summary>
    public async Task<TransactionResponse> CreateTransactionAsync(Guid cardId, CreateTransactionRequest request)
    {
        var card = await _cardRepository.GetByIdAsync(cardId)
            ?? throw new KeyNotFoundException($"Card {cardId} not found.");

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CardId = card.Id,
            Description = request.Description,
            TransactionDate = request.TransactionDate,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            CreatedAt = DateTime.UtcNow
        };

        await _transactionRepository.AddAsync(transaction);
        _logger.LogInformation("Transaction {TxId} stored for card {CardId}", transaction.Id, cardId);

        return MapToResponse(transaction);
    }

    /// <summary>
    /// Req 3: Retrieve a transaction converted to the specified currency (ISO 4217).
    ///
    /// Uses the exchange rate active on or before the transaction date (6 calendar months, inclusive).
    /// This is point-in-time valuation — see README: "Historical vs Current FX Rates".
    ///
    /// The FX service encapsulates Treasury API internals and USD pivot. Business logic here:
    /// convertedAmount = originalAmount * directRate.
    /// </summary>
    public async Task<ConvertedTransactionResponse> GetConvertedTransactionAsync(Guid transactionId, string currency)
    {
        var transaction = await _transactionRepository.GetByIdAsync(transactionId)
            ?? throw new KeyNotFoundException($"Transaction {transactionId} not found.");

        if (string.Equals(transaction.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase))
        {
            return new ConvertedTransactionResponse
            {
                Id = transaction.Id,
                Description = transaction.Description,
                TransactionDate = transaction.TransactionDate,
                OriginalAmount = transaction.Amount,
                OriginalCurrency = transaction.CurrencyCode,
                ExchangeRate = 1m,
                ConvertedAmount = transaction.Amount,
                Currency = currency,
                ExchangeRateDate = transaction.TransactionDate
            };
        }

        var rateEntry = await _fxService.GetHistoricalRateAsync(
                transaction.CurrencyCode, currency, transaction.TransactionDate)
            ?? throw new InvalidOperationException(
                $"Transaction {transactionId} cannot be converted to '{currency}': " +
                $"no exchange rate available within 6 months on or before " +
                $"{transaction.TransactionDate:yyyy-MM-dd}.");

        var convertedAmount = Math.Round(transaction.Amount * rateEntry.Rate, 2);

        _logger.LogInformation(
            "Transaction {TxId}: {Amount} {From} -> {Converted} {To} (rate {Rate}, dated {Date})",
            transactionId, transaction.Amount, transaction.CurrencyCode,
            convertedAmount, currency, rateEntry.Rate, rateEntry.RecordDate);

        return new ConvertedTransactionResponse
        {
            Id = transaction.Id,
            Description = transaction.Description,
            TransactionDate = transaction.TransactionDate,
            OriginalAmount = transaction.Amount,
            OriginalCurrency = transaction.CurrencyCode,
            ExchangeRate = rateEntry.Rate,
            ConvertedAmount = convertedAmount,
            Currency = currency,
            ExchangeRateDate = rateEntry.RecordDate
        };
    }

    private static TransactionResponse MapToResponse(Transaction t) => new()
    {
        Id = t.Id,
        CardId = t.CardId,
        Description = t.Description,
        TransactionDate = t.TransactionDate,
        Amount = t.Amount,
        CurrencyCode = t.CurrencyCode,
        CreatedAt = t.CreatedAt
    };
}
