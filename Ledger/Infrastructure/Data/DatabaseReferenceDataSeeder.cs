using System.Text.Json;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Services;
using Ledger.Features.Security.Models;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Service assurant l'initialisation des paramètres applicatifs par défaut, des polices et des données de référence utilisateur.
/// </summary>
public sealed class DatabaseReferenceDataSeeder(ILogger<DatabaseReferenceDataSeeder> logger)
{
    /// <summary>
    /// Initialise les données de référence globales et par utilisateur (devises, polices, catégories et types de comptes).
    /// </summary>
    public async Task SeedReferenceDataAsync(AppDbContext context, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        // 1. Marquer automatiquement la catégorie existante de salaire/revenus d'activité si elle n'est pas encore flaggée
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE Categories SET IsSalary = 1 WHERE (Name LIKE '%Salaire%' OR Name LIKE '%Revenus d''activit%') AND IsSalary = 0;",
            cancellationToken).ConfigureAwait(false);

        // 2. Initialisation des polices et des paramètres d'apparence
        var uiSettingsService = serviceProvider.GetService<IUiSettingsService>();
        if (uiSettingsService != null)
        {
            await uiSettingsService.EnsureDefaultFontsAndSettingsAsync(cancellationToken).ConfigureAwait(false);
        }

        // 3. Initialisation des devises configurables dans AppSettings si absent
        var currenciesSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Currencies:Supported", cancellationToken).ConfigureAwait(false);
        if (currenciesSetting == null)
        {
            context.AppSettings.Add(new AppSettingEntity
            {
                Key = "Currencies:Supported",
                Value = JsonSerializer.Serialize(CurrencyService.DefaultCurrencies),
                Description = "Liste des devises monétaires configurables du système",
                UpdatedAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // 4. Initialisation des catégories et données de référence par défaut pour les utilisateurs existants
        var categorySeedService = serviceProvider.GetService<ICategorySeedService>();
        var accountTypeService = serviceProvider.GetService<IAccountTypeService>();
        var transactionStatusService = serviceProvider.GetService<ITransactionStatusService>();
        var userManager = serviceProvider.GetService<UserManager<ApplicationUser>>();

        // Nettoyage au démarrage des doublons non utilisés
        if (accountTypeService != null)
        {
            await accountTypeService.DeduplicateAccountTypesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (transactionStatusService != null)
        {
            await transactionStatusService.DeduplicateStatusesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (userManager != null)
        {
            var existingUserIds = await userManager.Users.Select(u => u.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var userId in existingUserIds)
            {
                if (categorySeedService != null)
                {
                    await categorySeedService.EnsureDefaultCategoriesForUserAsync(userId, cancellationToken).ConfigureAwait(false);
                }

                if (accountTypeService != null)
                {
                    await accountTypeService.EnsureDefaultAccountTypesForUserAsync(userId, cancellationToken).ConfigureAwait(false);
                }

                if (transactionStatusService != null)
                {
                    await transactionStatusService.EnsureDefaultStatusesForUserAsync(userId, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        logger.LogInformation("Données de référence et paramètres applicatifs initialisés avec succès.");
    }
}
