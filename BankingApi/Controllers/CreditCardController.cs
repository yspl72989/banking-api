using BankingApi.Models.Requests;
using BankingApi.Models.Responses;
using BankingApi.Services.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.Controllers;

[ApiController]
[Route("api/cards")]
[Produces("application/json")]
public class CreditCardController : ControllerBase
{
    private readonly ICreditCardService _creditCardService;
    private readonly ILogger<CreditCardController> _logger;

    public CreditCardController(ICreditCardService creditCardService, ILogger<CreditCardController> logger)
    {
        _creditCardService = creditCardService;
        _logger = logger;
    }

    /// <summary>Creates a new credit card for a cardholder.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CardResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCard([FromBody] CreateCardRequest request)
    {
        var card = await _creditCardService.CreateCardAsync(request);

        var response = MapToCardResponse(card);
        return CreatedAtAction(nameof(GetCard), new { cardId = card.Id }, response);
    }

    /// <summary>Retrieves credit card details by card ID.</summary>
    [HttpGet("{cardId:guid}")]
    [ProducesResponseType(typeof(CardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCard(Guid cardId)
    {
        var card = await _creditCardService.GetCardAsync(cardId);

        if (card == null)
            return NotFound(new { message = $"Card {cardId} not found." });

        return Ok(MapToCardResponse(card));
    }

    /// <summary>Retrieves the current balance and credit utilisation for a card.</summary>
    [HttpGet("{cardId:guid}/balance")]
    [ProducesResponseType(typeof(BalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance(Guid cardId)
    {
        var card = await _creditCardService.GetCardAsync(cardId);

        if (card == null)
            return NotFound(new { message = $"Card {cardId} not found." });

        var usedCredit = card.CreditLimit - card.AvailableCredit;
        var utilisationPercent = card.CreditLimit > 0
            ? Math.Round(usedCredit / card.CreditLimit * 100, 2)
            : 0;

        var response = new BalanceResponse
        {
            CardId = card.Id,
            CardHolderName = card.CardHolderName,
            Currency = card.Currency,
            CreditLimit = card.CreditLimit,
            AvailableCredit = card.AvailableCredit,
            UsedCredit = usedCredit,
            UtilisationPercent = utilisationPercent,
            AsAt = DateTime.UtcNow
        };

        return Ok(response);
    }

    private static CardResponse MapToCardResponse(Models.Entities.CreditCard card) => new()
    {
        Id = card.Id,
        CardNumber = MaskCardNumber(card.CardNumber),
        CardHolderName = card.CardHolderName,
        ExpiryDate = card.ExpiryDate,
        CreditLimit = card.CreditLimit,
        AvailableCredit = card.AvailableCredit,
        Currency = card.Currency,
        IsActive = card.IsActive,
        CreatedAt = card.CreatedAt
    };

    /// <summary>Masks all but the last 4 digits, e.g. 4XXX-XXXX-XXXX-1234 → ****-****-****-1234</summary>
    private static string MaskCardNumber(string cardNumber) =>
        cardNumber.Length >= 4
            ? $"****-****-****-{cardNumber[^4..]}"
            : cardNumber;
}
