using BankingApi.Models.Entities;
using BankingApi.Models.Requests;

namespace BankingApi.Services.Contracts;

public interface ICreditCardService
{
    Task<CreditCard> CreateCardAsync(CreateCardRequest request);
    Task<CreditCard?> GetCardAsync(Guid cardId);
    IEnumerable<CreditCard> GetAllCards();
    Task<bool> DebitAsync(Guid cardId, decimal amountInCardCurrency);
    Task CreditAsync(Guid cardId, decimal amountInCardCurrency);
}
