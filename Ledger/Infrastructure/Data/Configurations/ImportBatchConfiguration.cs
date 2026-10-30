using Ledger.Features.Imports.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Infrastructure.Data.Configurations;

internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> entity)
    {
        entity.HasIndex(e => new { e.UserId, e.BankAccountId });
        entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
        entity.Property(e => e.Format).HasMaxLength(10).IsRequired();
    }
}
