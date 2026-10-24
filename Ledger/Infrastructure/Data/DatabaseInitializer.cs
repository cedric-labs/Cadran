using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Orchestrateur d'initialisation de la base SQLite au démarrage (synchronisation de schéma, données de référence et traductions).
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Initialise la base de données de manière idempotente : schéma SQLite, données de référence globales,
    /// traductions multilingues et normalisation des GUIDs.
    /// </summary>
    /// <param name="serviceProvider">Fournisseur de services applicatifs.</param>
    /// <param name="cancellationToken">Jeton d'annulation optionnel.</param>
    public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("DatabaseInitializer");

        try
        {
            var schemaSynchronizer = new DatabaseSchemaSynchronizer(loggerFactory.CreateLogger<DatabaseSchemaSynchronizer>());
            var referenceDataSeeder = new DatabaseReferenceDataSeeder(loggerFactory.CreateLogger<DatabaseReferenceDataSeeder>());
            var translationSeeder = new EntityTranslationSeeder(loggerFactory.CreateLogger<EntityTranslationSeeder>());

            // 1. Synchronisation du schéma SQLite et tables/colonnes complémentaires
            await schemaSynchronizer.SynchronizeSchemaAsync(context, cancellationToken);

            // 2. Initialisation des données de référence globales et par utilisateur
            await referenceDataSeeder.SeedReferenceDataAsync(context, scope.ServiceProvider, cancellationToken);

            // 3. Initialisation des traductions par défaut
            await translationSeeder.SeedDefaultTranslationsAsync(context, cancellationToken);

            // 4. Normalisation de la casse des identifiants GUID dans SQLite
            await DatabaseSchemaSynchronizer.NormalizeGuidCasingAsync(context, cancellationToken);

            logger.LogInformation("Base SQLite initialisée avec succès en mode WAL, polices, traductions et GUIDs normalisés.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Erreur lors de l'initialisation de la base SQLite.", ex);
        }
    }
}
