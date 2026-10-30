using Ledger.Features.Accounts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.Name });
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Bic).HasMaxLength(20);
        entity.Property(e => e.ColorHex).HasMaxLength(7);
    }
}
