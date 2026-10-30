using Ledger.Features.Accounts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class TransactionSplitConfiguration : IEntityTypeConfiguration<TransactionSplit>
{
    public void Configure(EntityTypeBuilder<TransactionSplit> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.TransactionId });
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.Notes).HasMaxLength(250);

        entity.HasOne(e => e.Transaction)
              .WithMany(t => t.Splits)
              .HasForeignKey(e => e.TransactionId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Category)
              .WithMany()
              .HasForeignKey(e => e.CategoryId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.Merchant)
              .WithMany()
              .HasForeignKey(e => e.MerchantId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
