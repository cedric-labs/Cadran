using Ledger.Features.Imports.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class CsvMappingProfileConfiguration : IEntityTypeConfiguration<CsvMappingProfile>
{
    public void Configure(EntityTypeBuilder<CsvMappingProfile> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.Name });
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Delimiter).HasMaxLength(5).HasDefaultValue(";");
        entity.Property(e => e.DecimalSeparator).HasMaxLength(2).HasDefaultValue(",");
        entity.Property(e => e.DateFormat).HasMaxLength(20).HasDefaultValue("dd/MM/yyyy");
    }
}
