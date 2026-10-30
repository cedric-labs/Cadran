using System.Reflection;
using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Imports.Entities;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Reports.Entities;
using Ledger.Features.RulesEngine.Entities;
using Ledger.Features.Security.Context;
using Ledger.Features.Security.Models;
using Ledger.Infrastructure.Configuration;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Contexte de base de données principal de l'application.
/// Intègre ASP.NET Core Identity, l'isolation multi-utilisateur stricte
/// via Global Query Filters et la persistance SQLite haute performance.
/// </summary>
public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUserService currentUserService) : IdentityDbContext<ApplicationUser>(options)
{
    // Entités Métier
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionSplit> TransactionSplits => Set<TransactionSplit>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<CategorizationRule> CategorizationRules => Set<CategorizationRule>();
    public DbSet<CsvMappingProfile> CsvMappingProfiles => Set<CsvMappingProfile>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<AppSettingEntity> AppSettings => Set<AppSettingEntity>();
    public DbSet<TransactionCustomStatus> TransactionStatuses => Set<TransactionCustomStatus>();
    public DbSet<CustomAccountType> CustomAccountTypes => Set<CustomAccountType>();
    public DbSet<UiFont> UiFonts => Set<UiFont>();
    public DbSet<EntityTranslation> EntityTranslations => Set<EntityTranslation>();
    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();
    public DbSet<ReportDashboard> ReportDashboards => Set<ReportDashboard>();
    public DbSet<ReportWidget> ReportWidgets => Set<ReportWidget>();
    public DbSet<SavedPivotReport> SavedPivotReports => Set<SavedPivotReport>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var modelBuilder = builder;

        // 1. Initialisation d'Identity
        base.OnModelCreating(builder);

        // 2. Application dynamique du filtre global de requêtes multi-utilisateur
        // Chaque entité implémentant IUserOwnedEntity ne renvoie STRICTEMENT que les données
        // du propriétaire connecté.
        foreach (var entityType in builder.Model.GetEntityTypes().Where(e => typeof(IUserOwnedEntity).IsAssignableFrom(e.ClrType)))
        {
            var method = typeof(AppDbContext)
                .GetMethod(nameof(ConfigureUserGlobalFilter), BindingFlags.Public | BindingFlags.Instance)?
                .MakeGenericMethod(entityType.ClrType);

            method?.Invoke(this, new object[] { builder });
        }

        // 3. Configurations relationnelles et contraintes d'intégrité
        modelBuilder.Entity<AppSettingEntity>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128);
        });

        modelBuilder.Entity<UiFont>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.FontFamilyCss).HasMaxLength(250).IsRequired();
            entity.Property(e => e.GoogleFontUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Bank>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Name });
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Bic).HasMaxLength(20);
            entity.Property(e => e.ColorHex).HasMaxLength(7);
        });

        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Name });
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Iban).HasMaxLength(34);
            entity.Property(e => e.OfxAccountId).HasMaxLength(100);
            entity.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("EUR");
            entity.Property(e => e.InitialBalance).HasPrecision(18, 2);

            entity.HasOne(e => e.Bank)
                  .WithMany(b => b.Accounts)
                  .HasForeignKey(e => e.BankId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CustomAccountType)
                  .WithMany()
                  .HasForeignKey(e => e.CustomAccountTypeId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CustomAccountType>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Name });
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.ColorHex).HasMaxLength(7).HasDefaultValue("#0dcaf0");
            entity.Property(e => e.IconName).HasMaxLength(50).HasDefaultValue("bi-piggy-bank");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.BankAccountId, e.Date });
            entity.HasIndex(e => new { e.UserId, e.Date });
            entity.HasIndex(e => new { e.UserId, e.BudgetDate });
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Label).HasMaxLength(250).IsRequired();
            entity.Property(e => e.RawLabel).HasMaxLength(500);
            entity.Property(e => e.Reference).HasMaxLength(100);

            entity.HasOne(e => e.BankAccount)
                  .WithMany(a => a.Transactions)
                  .HasForeignKey(e => e.BankAccountId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.CustomStatus)
                  .WithMany()
                  .HasForeignKey(e => e.CustomStatusId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Merchant)
                  .WithMany()
                  .HasForeignKey(e => e.MerchantId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TransactionSplit>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.TransactionId });
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(250);

            entity.HasOne(e => e.Transaction)
                  .WithMany(t => t.Splits)
                  .HasForeignKey(e => e.TransactionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Merchant)
                  .WithMany()
                  .HasForeignKey(e => e.MerchantId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Merchant>(entity =>
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
        });

        modelBuilder.Entity<TransactionCustomStatus>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Name });
            entity.Property(e => e.Name).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.ColorHex).HasMaxLength(7).HasDefaultValue("#3b82f6");
            entity.Property(e => e.IconName).HasMaxLength(50).HasDefaultValue("bi-check-circle");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Name }).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(80).IsRequired();
            entity.Property(e => e.IconName).HasMaxLength(50).HasDefaultValue("bi-tag");
            entity.Property(e => e.ColorHex).HasMaxLength(7).HasDefaultValue("#6366f1");

            entity.HasOne(e => e.ParentCategory)
                  .WithMany(p => p.SubCategories)
                  .HasForeignKey(e => e.ParentCategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CategorizationRule>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Priority, e.IsActive });
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Pattern).HasMaxLength(250).IsRequired();
            entity.Property(e => e.MinAmount).HasPrecision(18, 2);
            entity.Property(e => e.MaxAmount).HasPrecision(18, 2);

            entity.HasOne(e => e.TargetCategory)
                  .WithMany()
                  .HasForeignKey(e => e.TargetCategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetMerchant)
                  .WithMany()
                  .HasForeignKey(e => e.TargetMerchantId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.TargetBankAccount)
                  .WithMany()
                  .HasForeignKey(e => e.TargetBankAccountId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CsvMappingProfile>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Name });
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Delimiter).HasMaxLength(5).HasDefaultValue(";");
            entity.Property(e => e.DecimalSeparator).HasMaxLength(2).HasDefaultValue(",");
            entity.Property(e => e.DateFormat).HasMaxLength(20).HasDefaultValue("dd/MM/yyyy");
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.BankAccountId });
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Format).HasMaxLength(10).IsRequired();
        });

        modelBuilder.Entity<EntityTranslation>(entity =>
        {
            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.LanguageCode, e.Field }).IsUnique();
            entity.Property(e => e.EntityType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.LanguageCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Field).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Translation).HasMaxLength(255).IsRequired();
        });

        modelBuilder.Entity<RecurringExpense>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Name });
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Pattern).HasMaxLength(150).IsRequired();
            entity.Property(e => e.ExpectedAmount).HasPrecision(18, 2);
            entity.Property(e => e.AmountTolerancePercentage).HasPrecision(5, 2);

            entity.HasOne(e => e.TargetCategory)
                  .WithMany()
                  .HasForeignKey(e => e.TargetCategoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.TargetBankAccount)
                  .WithMany()
                  .HasForeignKey(e => e.TargetBankAccountId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ReportDashboard>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.DisplayOrder });
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();

            entity.HasMany(e => e.Widgets)
                  .WithOne(w => w.Dashboard)
                  .HasForeignKey(w => w.DashboardId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportWidget>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(100).IsRequired();
            entity.Property(e => e.WidgetType).HasMaxLength(50).IsRequired();
        });
    }

    /// <summary>
    /// Méthode générique appelée par réflexion pour attacher le filtre global sur le UserId.
    /// </summary>
    public void ConfigureUserGlobalFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IUserOwnedEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            currentUserService.UserId != null && e.UserId == currentUserService.UserId);
    }

    /// <summary>
    /// Intercepte la sauvegarde pour renseigner automatiquement le UserId
    /// des nouvelles entités si non spécifié, et mettre à jour les horodatages.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = currentUserService.UserId;
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IUserOwnedEntity userOwned && entry.State == EntityState.Added &&
                string.IsNullOrEmpty(userOwned.UserId) && !string.IsNullOrEmpty(currentUserId))
            {
                userOwned.UserId = currentUserId;
            }

            if (entry.Entity is BaseEntity baseEntity)
            {
                if (entry.State == EntityState.Added && baseEntity.CreatedAtUtc == default)
                {
                    baseEntity.CreatedAtUtc = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    baseEntity.UpdatedAtUtc = now;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
