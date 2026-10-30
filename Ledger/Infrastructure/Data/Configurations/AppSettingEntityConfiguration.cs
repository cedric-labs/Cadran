using Ledger.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class AppSettingEntityConfiguration : IEntityTypeConfiguration<AppSettingEntity>
{
    public void Configure(EntityTypeBuilder<AppSettingEntity> entity)
    {
        entity.HasKey(e => e.Key);
        entity.Property(e => e.Key).HasMaxLength(128);
    }
}
