using Ledger.Features.Accounts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class CustomAccountTypeConfiguration : IEntityTypeConfiguration<CustomAccountType>
{
    public void Configure(EntityTypeBuilder<CustomAccountType> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.Name });
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Code).HasMaxLength(50);
        entity.Property(e => e.Description).HasMaxLength(250);
        entity.Property(e => e.ColorHex).HasMaxLength(7).HasDefaultValue("#0dcaf0");
        entity.Property(e => e.IconName).HasMaxLength(50).HasDefaultValue("bi-piggy-bank");
    }
}
