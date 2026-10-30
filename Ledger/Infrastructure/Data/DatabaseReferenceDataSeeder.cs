using Ledger.Features.Settings.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Service assurant l'initialisation des paramètres applicatifs par défaut, des polices et des données de référence utilisateur.
/// </summary>
public sealed class DatabaseReferenceDataSeeder(ILogger<DatabaseReferenceDataSeeder> logger, TimeProvider timeProvider)
{
    private readonly SupportedCurrenciesSettingInitializer _currenciesSettingInitializer = new(timeProvider);

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
        await _currenciesSettingInitializer.EnsureAsync(context, cancellationToken).ConfigureAwait(false);

        // 4. Initialisation des catégories et données de référence par défaut pour les utilisateurs existants
        await UserReferenceDataSeeder.SeedAsync(serviceProvider, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Données de référence et paramètres applicatifs initialisés avec succès.");
    }
}
