namespace Ledger.Features.Security.Services;

/// <summary>
/// Stockage local sécurisé de la clé SQLite pour la fonction "Se souvenir sur cet appareil".
/// Délègue son implémentation au framework partagé <see cref="SqliteForge.Security.LocalProtectedFileKeyStore"/>.
/// </summary>
/// <param name="customPath">Chemin personnalisé optionnel du fichier de clé.</param>
public sealed class LocalProtectedFileKeyStore(string? customPath = null)
    : SqliteForge.Security.LocalProtectedFileKeyStore(customPath, "Cadran"), IDatabaseKeyStore
{
    /// <inheritdoc />
    public Task<string?> GetSavedKeyAsync() => base.GetSavedKeyAsync(CancellationToken.None);

    /// <inheritdoc />
    public Task SaveKeyAsync(string key) => base.SaveKeyAsync(key, CancellationToken.None);

    /// <inheritdoc />
    public Task ClearSavedKeyAsync() => base.ClearSavedKeyAsync(CancellationToken.None);

    /// <inheritdoc />
    public Task<bool> HasSavedKeyAsync() => base.HasSavedKeyAsync(CancellationToken.None);
}
