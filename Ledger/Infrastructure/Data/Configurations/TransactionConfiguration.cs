using Ledger.Features.Accounts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.BankAccountId, e.Date });
        entity.HasIndex(e => new { e.UserId, e.Date });
        entity.HasIndex(e => new { e.UserId, e.BudgetDate });
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.Label).HasMaxLength(250).IsRequired();
        entity.Property(e => e.RawLabel).HasMaxLength(500);
        entity.Property(e => e.Reference).HasMaxLength(100);

        entity.HasOne(e => e.BankAccount)
              .WithMany(a => a.Transactions)
              .HasForeignKey(e => e.BankAccountId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Category)
              .WithMany()
              .HasForeignKey(e => e.CategoryId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.CustomStatus)
              .WithMany()
              .HasForeignKey(e => e.CustomStatusId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.Merchant)
              .WithMany()
              .HasForeignKey(e => e.MerchantId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
