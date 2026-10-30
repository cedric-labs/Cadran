using Ledger.Features.Accounts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.Name });
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Iban).HasMaxLength(34);
        entity.Property(e => e.OfxAccountId).HasMaxLength(100);
        entity.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("EUR");
        entity.Property(e => e.InitialBalance).HasPrecision(18, 2);

        entity.HasOne(e => e.Bank)
              .WithMany(b => b.Accounts)
              .HasForeignKey(e => e.BankId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CustomAccountType)
              .WithMany()
              .HasForeignKey(e => e.CustomAccountTypeId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
