using BankingApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Data;

public class BankingDbContext : DbContext
{
    public BankingDbContext(DbContextOptions<BankingDbContext> options) : base(options) { }

    public DbSet<Card> Cards { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CreditLimit).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(c => c.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Description).HasMaxLength(500).IsRequired();
            entity.Property(t => t.Amount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(t => t.TransactionDate).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();

            entity.HasOne(t => t.Card)
                  .WithMany(c => c.Transactions)
                  .HasForeignKey(t => t.CardId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
