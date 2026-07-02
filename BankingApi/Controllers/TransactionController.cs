using BankingApi.Models.Requests;
using BankingApi.Models.Responses;
using BankingApi.Services.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.Controllers;

[ApiController]
[Route("api/cards/{cardId:guid}/transactions")]
[Produces("application/json")]
public class TransactionController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly ICreditCardService _creditCardService;
    private readonly ILogger<TransactionController> _logger;

    public TransactionController(
        ITransactionService transactionService,
        ICreditCardService creditCardService,
        ILogger<TransactionController> logger)
    {
        _transactionService = transactionService;
        _creditCardService = creditCardService;
        _logger = logger;
    }

    /// <summary>
    /// Processes a purchase transaction on the specified card.
    /// If the transaction currency differs from the card currency, the Treasury FX API
    /// is called to convert the amount at the live exchange rate.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProcessTransaction(Guid cardId, [FromBody] ProcessTransactionRequest request)
    {
        var card = await _creditCardService.GetCardAsync(cardId);
        if (card == null)
            return NotFound(new { message = $"Card {cardId} not found." });

        try
        {
            var transaction = await _transactionService.ProcessPurchaseAsync(cardId, request);
            return Ok(MapToResponse(transaction));
        }
        catch (InvalidOperationException ex)
        {
            // FX service failure
            _logger.LogWarning(ex, "FX service error processing transaction on card {CardId}", cardId);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    /// <summary>Returns all transactions for the specified card, most recent first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactions(Guid cardId)
    {
        var card = await _creditCardService.GetCardAsync(cardId);
        if (card == null)
            return NotFound(new { message = $"Card {cardId} not found." });

        var transactions = _transactionService.GetTransactionsForCard(cardId);
        return Ok(transactions.Select(MapToResponse));
    }

    private static TransactionResponse MapToResponse(Models.Entities.Transaction t) => new()
    {
        Id = t.Id,
        CardId = t.CardId,
        Amount = t.Amount,
        Currency = t.Currency,
        ExchangeRate = t.ExchangeRate,
        AmountInCardCurrency = t.AmountInCardCurrency,
        Description = t.Description,
        Type = t.Type.ToString(),
        Status = t.Status.ToString(),
        DeclineReason = t.DeclineReason,
        ProcessedAt = t.ProcessedAt
    };
}
