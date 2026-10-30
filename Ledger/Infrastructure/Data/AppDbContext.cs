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
    ICurrentUserService currentUserService,
    TimeProvider? timeProvider = null) : IdentityDbContext<ApplicationUser>(options)
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

        // 3. Configurations relationnelles et contraintes d'intégrité, une classe par entité
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
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
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

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
