using BankingApi.Data;
using BankingApi.Domain;
using BankingApi.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Repositories;

public class CardRepository : ICardRepository
{
    private readonly BankingDbContext _context;

    public CardRepository(BankingDbContext context)
    {
        _context = context;
    }

    public async Task<Card> AddAsync(Card card)
    {
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();
        return card;
    }

    public Task<Card?> GetByIdAsync(Guid id)
        => _context.Cards.FirstOrDefaultAsync(c => c.Id == id);

    public Task<Card?> GetByIdWithTransactionsAsync(Guid id)
        => _context.Cards
            .Include(c => c.Transactions)
            .FirstOrDefaultAsync(c => c.Id == id);
}
