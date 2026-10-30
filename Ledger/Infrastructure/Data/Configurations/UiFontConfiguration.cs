using Ledger.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class UiFontConfiguration : IEntityTypeConfiguration<UiFont>
{
    public void Configure(EntityTypeBuilder<UiFont> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.FontFamilyCss).HasMaxLength(250).IsRequired();
        entity.Property(e => e.GoogleFontUrl).HasMaxLength(500);
    }
}
