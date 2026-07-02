using BankingApi.Domain;

namespace BankingApi.Repositories.Contracts;

public interface ICardRepository
{
    Task<Card> AddAsync(Card card);
    Task<Card?> GetByIdAsync(Guid id);
    Task<Card?> GetByIdWithTransactionsAsync(Guid id);
}
