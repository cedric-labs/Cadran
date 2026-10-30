using Ledger.Features.Reports.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class ReportWidgetConfiguration : IEntityTypeConfiguration<ReportWidget>
{
    public void Configure(EntityTypeBuilder<ReportWidget> entity)
    {
        entity.Property(e => e.Title).HasMaxLength(100).IsRequired();
        entity.Property(e => e.WidgetType).HasMaxLength(50).IsRequired();
    }
}
