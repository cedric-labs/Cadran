using Ledger.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class EntityTranslationConfiguration : IEntityTypeConfiguration<EntityTranslation>
{
    public void Configure(EntityTypeBuilder<EntityTranslation> entity)
    {
        entity.HasIndex(e => new { e.EntityType, e.EntityId, e.LanguageCode, e.Field }).IsUnique();
        entity.Property(e => e.EntityType).HasMaxLength(50).IsRequired();
        entity.Property(e => e.LanguageCode).HasMaxLength(10).IsRequired();
        entity.Property(e => e.Field).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Translation).HasMaxLength(255).IsRequired();
    }
}
