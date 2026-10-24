namespace Ledger.Features.Security.Services;

/// <summary>
/// Contrat du magasin local de persistance sécurisée de la clé ("Se souvenir sur cet appareil").
/// </summary>
public interface IDatabaseKeyStore
{
    /// <summary>
    /// Récupère la phrase de passe sauvegardée pour cet appareil, ou null si absente ou non déchiffrable.
    /// </summary>
    Task<string?> GetSavedKeyAsync();

    /// <summary>
    /// Sauvegarde de manière protégée la phrase de passe sur cet appareil.
    /// </summary>
    Task SaveKeyAsync(string key);

    /// <summary>
    /// Supprime la phrase de passe mémorisée sur cet appareil.
    /// </summary>
    Task ClearSavedKeyAsync();

    /// <summary>
    /// Indique si une clé sauvegardée existe sur cet appareil.
    /// </summary>
    Task<bool> HasSavedKeyAsync();
}
