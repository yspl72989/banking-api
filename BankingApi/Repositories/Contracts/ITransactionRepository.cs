using BankingApi.Domain;

namespace BankingApi.Repositories.Contracts;

public interface ITransactionRepository
{
    Task<Transaction> AddAsync(Transaction transaction);
    Task<Transaction?> GetByIdAsync(Guid id);
}
