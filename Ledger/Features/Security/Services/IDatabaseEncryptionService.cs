namespace Ledger.Features.Security.Services;

/// <summary>
/// Service de gestion du chiffrement global de la base SQLite (SQLCipher / SQLite3 Multiple Ciphers).
/// </summary>
public interface IDatabaseEncryptionService
{
    /// <summary>
    /// Indique si la base de données physique SQLite est actuellement chiffrée.
    /// </summary>
    bool IsDatabaseEncrypted();

    /// <summary>
    /// Indique si la base de données est chiffrée et verrouillée (en attente du mot de passe maître).
    /// </summary>
    bool IsDatabaseLocked();

    /// <summary>
    /// Vérifie si une phrase de passe permet de déchiffrer la base de données.
    /// </summary>
    bool VerifyPassphrase(string passphrase);

    /// <summary>
    /// Déverrouille la base de données au démarrage ou après verrouillage.
    /// </summary>
    Task<bool> UnlockDatabaseAsync(string passphrase, bool rememberOnDevice);

    /// <summary>
    /// Chiffre pour la première fois une base de données existante non chiffrée.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> EncryptDatabaseAsync(string newPassphrase, bool rememberOnDevice);

    /// <summary>
    /// Modifie la phrase de passe de chiffrement de la base (opération PRAGMA rekey).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> ChangePassphraseAsync(string currentPassphrase, string newPassphrase, bool rememberOnDevice);

    /// <summary>
    /// Retire le chiffrement pour restaurer la base au format SQLite standard non chiffré.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> DecryptDatabaseToPlainAsync(string currentPassphrase);

    /// <summary>
    /// Indique si la mémorisation sur cet appareil est active.
    /// </summary>
    Task<bool> IsRememberOnDeviceEnabledAsync();

    /// <summary>
    /// Active ou désactive la mémorisation sur cet appareil avec la clé en mémoire.
    /// </summary>
    Task SetRememberOnDeviceAsync(bool remember);
}
