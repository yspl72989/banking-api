using BankingApi.Data;
using BankingApi.Domain;
using BankingApi.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly BankingDbContext _context;

    public TransactionRepository(BankingDbContext context)
    {
        _context = context;
    }

    public async Task<Transaction> AddAsync(Transaction transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public Task<Transaction?> GetByIdAsync(Guid id)
        => _context.Transactions.FirstOrDefaultAsync(t => t.Id == id);
}
