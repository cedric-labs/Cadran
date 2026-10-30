using Ledger.Features.Categories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.Name }).IsUnique();
        entity.Property(e => e.Name).HasMaxLength(80).IsRequired();
        entity.Property(e => e.IconName).HasMaxLength(50).HasDefaultValue("bi-tag");
        entity.Property(e => e.ColorHex).HasMaxLength(7).HasDefaultValue("#6366f1");

        entity.HasOne(e => e.ParentCategory)
              .WithMany(p => p.SubCategories)
              .HasForeignKey(e => e.ParentCategoryId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
