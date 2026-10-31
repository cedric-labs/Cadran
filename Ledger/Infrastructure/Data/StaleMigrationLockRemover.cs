using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Supprime le verrou de migration EF Core laissé dans la base par un démarrage interrompu.
/// </summary>
/// <remarks>
/// EF Core ne libère <c>__EFMigrationsLock</c> qu'en fin de migration : si le processus est arrêté entre-temps,
/// la ligne reste et chaque démarrage suivant attend indéfiniment. Aucune migration de l'application ne dure
/// plusieurs minutes, donc un verrou plus ancien que le délai est considéré comme orphelin.
/// </remarks>
public sealed class StaleMigrationLockRemover(ILogger<StaleMigrationLockRemover> logger, TimeProvider timeProvider)
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    // Même format que celui écrit par EF Core (DateTimeOffset en UTC) : l'ordre lexicographique suit l'ordre chronologique.
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fffffffzzz";

    /// <summary>
    /// Supprime le verrou de migration s'il est plus ancien que le délai toléré.
    /// </summary>
    /// <param name="context">Contexte de base de données.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns><c>true</c> si un verrou orphelin a été supprimé.</returns>
    public async Task<bool> RemoveIfStaleAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tableCount = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE name = '__EFMigrationsLock' AND type = 'table'")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tableCount == 0)
            return false;

        var cutoff = timeProvider.GetUtcNow().ToUniversalTime().Subtract(StaleAfter)
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var removed = await context.Database
            .ExecuteSqlAsync($"DELETE FROM \"__EFMigrationsLock\" WHERE \"Timestamp\" < {cutoff}", cancellationToken)
            .ConfigureAwait(false);
        if (removed == 0)
            return false;

        logger.LogWarning("Verrou de migration EF Core orphelin supprimé (plus ancien que {StaleAfter}) : un démarrage précédent a été interrompu pendant la migration.", StaleAfter);
        return true;
    }
}
