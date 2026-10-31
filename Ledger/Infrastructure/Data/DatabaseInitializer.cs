using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Orchestrateur d'initialisation de la base SQLite au démarrage (synchronisation de schéma, données de référence et traductions).
/// </summary>
public static class DatabaseInitializer
{
    private static readonly SemaphoreSlim InitializationLock = new(1, 1);

    /// <summary>
    /// Initialise la base de données de manière idempotente : schéma SQLite, données de référence globales,
    /// traductions multilingues et normalisation des GUIDs.
    /// </summary>
    /// <param name="serviceProvider">Fournisseur de services applicatifs.</param>
    /// <param name="cancellationToken">Jeton d'annulation optionnel.</param>
    public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        await InitializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("DatabaseInitializer");

            var schemaSynchronizer = new DatabaseSchemaSynchronizer(loggerFactory.CreateLogger<DatabaseSchemaSynchronizer>());
            var timeProvider = scope.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
            var referenceDataSeeder = new DatabaseReferenceDataSeeder(loggerFactory.CreateLogger<DatabaseReferenceDataSeeder>(), timeProvider);
            var translationSeeder = new EntityTranslationSeeder(loggerFactory.CreateLogger<EntityTranslationSeeder>());

            var staleLockRemover = new StaleMigrationLockRemover(loggerFactory.CreateLogger<StaleMigrationLockRemover>(), timeProvider);
            await staleLockRemover.RemoveIfStaleAsync(context, cancellationToken).ConfigureAwait(false);

            // 1. Synchronisation du schéma SQLite et tables/colonnes complémentaires
            await schemaSynchronizer.SynchronizeSchemaAsync(context, cancellationToken).ConfigureAwait(false);

            // 2. Initialisation des données de référence globales et par utilisateur
            await referenceDataSeeder.SeedReferenceDataAsync(context, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);

            // 3. Initialisation des traductions par défaut
            await translationSeeder.SeedDefaultTranslationsAsync(context, cancellationToken).ConfigureAwait(false);

            // 4. Normalisation de la casse des identifiants GUID dans SQLite
            await DatabaseSchemaSynchronizer.NormalizeGuidCasingAsync(context, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Base SQLite initialisée avec succès en mode WAL, polices, traductions et GUIDs normalisés.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("Erreur lors de l'initialisation de la base SQLite.", ex);
        }
        finally
        {
            InitializationLock.Release();
        }
    }
}
