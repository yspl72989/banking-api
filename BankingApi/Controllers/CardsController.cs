using BankingApi.Models.Requests;
using BankingApi.Models.Responses;
using BankingApi.Services.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.Controllers;

[ApiController]
[Route("api/cards")]
[Produces("application/json")]
public class CardsController : ControllerBase
{
    private readonly ICardService _cardService;
    private readonly ILogger<CardsController> _logger;

    public CardsController(ICardService cardService, ILogger<CardsController> logger)
    {
        _cardService = cardService;
        _logger = logger;
    }

    /// <summary>
    /// Requirement 1: Create a card with a credit limit.
    /// Returns the card's unique identifier and stored details.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CardResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCard([FromBody] CreateCardRequest request)
    {
        var card = await _cardService.CreateCardAsync(request);
        return CreatedAtAction(nameof(GetBalance), new { cardId = card.Id }, card);
    }

    /// <summary>
    /// Requirement 4: Retrieve the available balance of a card in a specified currency.
    /// Uses the latest available Treasury FX exchange rate.
    /// Specify currency as ISO 4217 code (e.g. "AUD", "EUR", "GBP").
    /// Defaults to "USD" (no FX conversion applied).
    /// </summary>
    [HttpGet("{cardId:guid}/balance")]
    [ProducesResponseType(typeof(BalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetBalance(Guid cardId, [FromQuery] string currency = "USD")
    {
        try
        {
            var balance = await _cardService.GetBalanceAsync(cardId, currency);
            return Ok(balance);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "FX rate unavailable for balance request on card {CardId}", cardId);
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
