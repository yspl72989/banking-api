using BankingApi.Models.Requests;
using BankingApi.Models.Responses;
using BankingApi.Services.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.Controllers;

[ApiController]
[Route("api/cards/{cardId:guid}/transactions")]
[Produces("application/json")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(ITransactionService transactionService, ILogger<TransactionsController> logger)
    {
        _transactionService = transactionService;
        _logger = logger;
    }

    /// <summary>
    /// Requirement 2: Store a purchase transaction associated with a specific card.
    /// Amount is stored in the transaction's original currency (ISO 4217, e.g. "USD", "AUD", "EUR").
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateTransaction(Guid cardId, [FromBody] CreateTransactionRequest request)
    {
        try
        {
            var transaction = await _transactionService.CreateTransactionAsync(cardId, request);
            return CreatedAtAction(nameof(GetTransaction), new { cardId, transactionId = transaction.Id }, transaction);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Requirement 3: Retrieve a stored transaction converted to a specified currency.
    /// Uses the exchange rate active on or before the transaction date (within 6 months).
    /// Returns HTTP 422 if no valid rate is found within the 6-month window.
    /// Specify currency as ISO 4217 code (e.g. "AUD", "EUR", "GBP").
    /// Defaults to "USD" (no FX conversion applied).
    /// </summary>
    [HttpGet("{transactionId:guid}")]
    [ProducesResponseType(typeof(ConvertedTransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetTransaction(Guid cardId, Guid transactionId, [FromQuery] string currency = "USD")
    {
        try
        {
            var transaction = await _transactionService.GetConvertedTransactionAsync(transactionId, currency);
            return Ok(transaction);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "FX rate unavailable for transaction {TransactionId}", transactionId);
            return UnprocessableEntity(new { message = ex.Message });
        }
    }
}
