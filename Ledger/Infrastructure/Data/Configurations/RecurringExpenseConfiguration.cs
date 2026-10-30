using Ledger.Features.Recurring.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class RecurringExpenseConfiguration : IEntityTypeConfiguration<RecurringExpense>
{
    public void Configure(EntityTypeBuilder<RecurringExpense> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.Name });
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Pattern).HasMaxLength(150).IsRequired();
        entity.Property(e => e.ExpectedAmount).HasPrecision(18, 2);
        entity.Property(e => e.AmountTolerancePercentage).HasPrecision(5, 2);

        entity.HasOne(e => e.TargetCategory)
              .WithMany()
              .HasForeignKey(e => e.TargetCategoryId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.TargetBankAccount)
              .WithMany()
              .HasForeignKey(e => e.TargetBankAccountId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
