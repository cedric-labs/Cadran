namespace Ledger.Features.Security.Context;

/// <summary>
/// Détenteur singleton thread-safe en mémoire de la clé de déchiffrement SQLite.
/// Délègue son implémentation au framework partagé <see cref="SqliteForge.Security.DatabaseKeyHolder"/>.
/// </summary>
public class DatabaseKeyHolder : IDatabaseKeyHolder
{
    private readonly SqliteForge.Security.DatabaseKeyHolder _inner = new();

    /// <inheritdoc />
    public string? EncryptionKey => _inner.EncryptionKey;

    /// <inheritdoc />
    public bool IsDatabaseEncrypted => _inner.IsDatabaseEncrypted;

    /// <inheritdoc />
    public bool IsUnlocked => _inner.IsUnlocked;

    /// <inheritdoc />
    public void SetKey(string? key, bool isEncrypted) => _inner.SetKey(key, isEncrypted);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();
}
