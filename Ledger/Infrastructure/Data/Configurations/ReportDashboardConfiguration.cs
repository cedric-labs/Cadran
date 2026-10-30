using Ledger.Features.Reports.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class ReportDashboardConfiguration : IEntityTypeConfiguration<ReportDashboard>
{
    public void Configure(EntityTypeBuilder<ReportDashboard> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.DisplayOrder });
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();

        entity.HasMany(e => e.Widgets)
              .WithOne(w => w.Dashboard)
              .HasForeignKey(w => w.DashboardId)
              .IsRequired(false)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
