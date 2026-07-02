using BankingApi.Models.Requests;
using BankingApi.Models.Responses;

namespace BankingApi.Services.Contracts;

public interface ITransactionService
{
    Task<TransactionResponse> CreateTransactionAsync(Guid cardId, CreateTransactionRequest request);
    Task<ConvertedTransactionResponse> GetConvertedTransactionAsync(Guid cardId, Guid transactionId, string currency);
}
