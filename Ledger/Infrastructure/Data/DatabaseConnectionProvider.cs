using Ledger.Features.Security.Context;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Implémentation du fournisseur dynamique de chaîne de connexion.
/// Délègue son implémentation au framework partagé <see cref="SqliteForge.Data.DatabaseConnectionProvider"/>.
/// </summary>
/// <param name="baseConnectionString">Chaîne de connexion SQLite de base.</param>
/// <param name="keyHolder">Détenteur en mémoire de la clé de déchiffrement.</param>
public sealed class DatabaseConnectionProvider(string baseConnectionString, IDatabaseKeyHolder keyHolder) : IDatabaseConnectionProvider
{
    private readonly SqliteForge.Data.DatabaseConnectionProvider _inner = new(baseConnectionString, keyHolder);

    /// <inheritdoc />
    public string GetConnectionString() => _inner.GetConnectionString();

    /// <inheritdoc />
    public string GetDatabaseFilePath() => _inner.GetDatabaseFilePath();

    /// <inheritdoc />
    public bool IsDatabaseEncrypted() => _inner.IsDatabaseEncrypted();

    /// <inheritdoc />
    public bool IsDatabaseLocked() => _inner.IsDatabaseLocked();
}
