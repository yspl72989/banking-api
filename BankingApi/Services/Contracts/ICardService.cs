using BankingApi.Models.Requests;
using BankingApi.Models.Responses;

namespace BankingApi.Services.Contracts;

public interface ICardService
{
    Task<CardResponse> CreateCardAsync(CreateCardRequest request);
    Task<BalanceResponse> GetBalanceAsync(Guid cardId, string currency);
}
