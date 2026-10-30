using Ledger.Features.Merchants.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.NormalizedName });
        entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
        entity.Property(e => e.NormalizedName).HasMaxLength(150).IsRequired();
        entity.Property(e => e.ColorHex).HasMaxLength(7).HasDefaultValue("#6366f1");
        entity.Property(e => e.IconName).HasMaxLength(50).HasDefaultValue("bi-shop");

        entity.HasOne(e => e.DefaultCategory)
              .WithMany()
              .HasForeignKey(e => e.DefaultCategoryId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
