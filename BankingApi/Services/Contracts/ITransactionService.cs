using BankingApi.Models.Entities;
using BankingApi.Models.Requests;

namespace BankingApi.Services.Contracts;

public interface ITransactionService
{
    Task<Transaction> ProcessPurchaseAsync(Guid cardId, ProcessTransactionRequest request);
    IEnumerable<Transaction> GetTransactionsForCard(Guid cardId);
}
