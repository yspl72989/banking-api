using BankingApi.Domain;
using BankingApi.Models.Requests;
using BankingApi.Models.Responses;
using BankingApi.Repositories.Contracts;
using BankingApi.Services.Contracts;

namespace BankingApi.Services;

/// <summary>
/// Handles Requirement 2 (store transaction) and Requirement 3 (retrieve with FX conversion).
/// Facade over ITransactionRepository, ICardRepository, and IFxService.
/// </summary>
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

    /// <summary>Req 2: Accept and store a purchase transaction for a specific card.</summary>
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
            CreatedAt = DateTime.UtcNow
        };

        await _transactionRepository.AddAsync(transaction);
        _logger.LogInformation("Transaction {TransactionId} stored for card {CardId}", transaction.Id, cardId);

        return MapToResponse(transaction);
    }

    /// <summary>
    /// Req 3: Retrieve a transaction with its amount converted to the specified currency,
    /// using the exchange rate active on or before the transaction date (within 6 months).
    /// </summary>
    public async Task<ConvertedTransactionResponse> GetConvertedTransactionAsync(Guid transactionId, string currency)
    {
        var transaction = await _transactionRepository.GetByIdAsync(transactionId)
            ?? throw new KeyNotFoundException($"Transaction {transactionId} not found.");

        var rate = await _fxService.GetRateOnOrBeforeDateAsync(currency, transaction.TransactionDate)
            ?? throw new InvalidOperationException(
                $"Transaction {transactionId} cannot be converted to '{currency}': " +
                $"no exchange rate is available within 6 months on or before {transaction.TransactionDate:yyyy-MM-dd}.");

        var convertedAmount = Math.Round(transaction.Amount * rate.Rate, 2);

        _logger.LogInformation(
            "Transaction {TransactionId} converted to {Currency}: {OriginalAmount} USD -> {Converted} at rate {Rate} (dated {RateDate})",
            transactionId, currency, transaction.Amount, convertedAmount, rate.Rate, rate.RecordDate);

        return new ConvertedTransactionResponse
        {
            Id = transaction.Id,
            Description = transaction.Description,
            TransactionDate = transaction.TransactionDate,
            OriginalAmount = transaction.Amount,
            ExchangeRate = rate.Rate,
            ConvertedAmount = convertedAmount,
            Currency = currency,
            ExchangeRateDate = rate.RecordDate
        };
    }

    private static TransactionResponse MapToResponse(Transaction t) => new()
    {
        Id = t.Id,
        CardId = t.CardId,
        Description = t.Description,
        TransactionDate = t.TransactionDate,
        Amount = t.Amount,
        CreatedAt = t.CreatedAt
    };
}
