using Ledger.Features.RulesEngine.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class CategorizationRuleConfiguration : IEntityTypeConfiguration<CategorizationRule>
{
    public void Configure(EntityTypeBuilder<CategorizationRule> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.Priority, e.IsActive });
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Pattern).HasMaxLength(250).IsRequired();
        entity.Property(e => e.MinAmount).HasPrecision(18, 2);
        entity.Property(e => e.MaxAmount).HasPrecision(18, 2);

        entity.HasOne(e => e.TargetCategory)
              .WithMany()
              .HasForeignKey(e => e.TargetCategoryId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.TargetMerchant)
              .WithMany()
              .HasForeignKey(e => e.TargetMerchantId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.TargetBankAccount)
              .WithMany()
              .HasForeignKey(e => e.TargetBankAccountId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
