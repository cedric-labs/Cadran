using Ledger.Features.Security.Context;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ledger.Features.Security.Services;

/// <summary>
/// Service gérant le cycle de vie du chiffrement SQLite avec SQLite3 Multiple Ciphers / SQLCipher.
/// Délègue les opérations techniques au framework partagé <see cref="SqliteForge.Security.SqliteDatabaseEncryptionService"/>.
/// </summary>
public sealed class DatabaseEncryptionService(
    IDatabaseConnectionProvider connectionProvider,
    IDatabaseKeyHolder keyHolder,
    IDatabaseKeyStore keyStore,
    IServiceProvider serviceProvider,
    ILogger<DatabaseEncryptionService> logger,
    IConfiguration? configuration = null) : IDatabaseEncryptionService
{
    private readonly SqliteForge.Security.SqliteDatabaseEncryptionService _innerService = new(
        Validate(connectionProvider),
        Validate(keyHolder),
        new KeyStoreAdapter(Validate(keyStore)),
        Validate(serviceProvider),
        Validate(logger),
        configuration,
        InitializeApplicationAfterUnlockAsync);

    private static T Validate<T>(T value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        return value;
    }

    /// <inheritdoc />
    public bool IsDatabaseEncrypted() => _innerService.IsDatabaseEncrypted();

    /// <inheritdoc />
    public bool IsDatabaseLocked() => _innerService.IsDatabaseLocked();

    /// <inheritdoc />
    public bool VerifyPassphrase(string passphrase) => _innerService.VerifyPassphrase(passphrase);

    /// <inheritdoc />
    public Task<bool> UnlockDatabaseAsync(string passphrase, bool rememberOnDevice) =>
        _innerService.UnlockDatabaseAsync(passphrase, rememberOnDevice, CancellationToken.None);

    /// <inheritdoc />
    public Task<(bool Success, string? ErrorMessage)> EncryptDatabaseAsync(string newPassphrase, bool rememberOnDevice) =>
        _innerService.EncryptDatabaseAsync(newPassphrase, rememberOnDevice, CancellationToken.None);

    /// <inheritdoc />
    public Task<(bool Success, string? ErrorMessage)> ChangePassphraseAsync(string currentPassphrase, string newPassphrase, bool rememberOnDevice) =>
        _innerService.ChangePassphraseAsync(currentPassphrase, newPassphrase, rememberOnDevice, CancellationToken.None);

    /// <inheritdoc />
    public Task<(bool Success, string? ErrorMessage)> DecryptDatabaseToPlainAsync(string currentPassphrase) =>
        _innerService.DecryptDatabaseToPlainAsync(currentPassphrase, CancellationToken.None);

    /// <inheritdoc />
    public Task<bool> IsRememberOnDeviceEnabledAsync() =>
        _innerService.IsRememberOnDeviceEnabledAsync(CancellationToken.None);

    /// <inheritdoc />
    public Task SetRememberOnDeviceAsync(bool remember) =>
        _innerService.SetRememberOnDeviceAsync(remember, CancellationToken.None);

    /// <summary>
    /// Détecte si un fichier physique SQLite est chiffré en analysant son en-tête de 16 octets.
    /// </summary>
    /// <param name="filePath">Chemin du fichier SQLite à inspecter.</param>
    /// <returns><c>true</c> si le fichier existe et est chiffré ; sinon, <c>false</c>.</returns>
    public static bool CheckIfFileEncrypted(string filePath) =>
        SqliteForge.Security.SqliteDatabaseEncryptionService.CheckIfFileEncrypted(filePath);

    /// <summary>
    /// Vérifie si une phrase de passe permet de déchiffrer et d'interroger la base de données.
    /// </summary>
    /// <param name="baseConnectionString">Chaîne de connexion de base.</param>
    /// <param name="passphrase">Phrase de passe à vérifier.</param>
    /// <returns><c>true</c> si la phrase de passe est valide ; sinon, <c>false</c>.</returns>
    public static bool VerifyPassphrase(string baseConnectionString, string passphrase) =>
        SqliteForge.Security.SqliteDatabaseEncryptionService.VerifyPassphrase(baseConnectionString, passphrase);

    private static async Task InitializeApplicationAfterUnlockAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<AppDbContext>();
        if (dbContext is not null)
        {
            await DatabaseInitializer.InitializeDatabaseAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        }

        var currencyService = scope.ServiceProvider.GetService<ICurrencyService>();
        if (currencyService is not null)
        {
            CurrencyExtensions.Configure(currencyService);
        }
    }

    private sealed class KeyStoreAdapter(IDatabaseKeyStore keyStore) : SqliteForge.Security.IDatabaseKeyStore
    {
        public Task<string?> GetSavedKeyAsync(CancellationToken cancellationToken = default) => keyStore.GetSavedKeyAsync();
        public Task SaveKeyAsync(string key, CancellationToken cancellationToken = default) => keyStore.SaveKeyAsync(key);
        public Task ClearSavedKeyAsync(CancellationToken cancellationToken = default) => keyStore.ClearSavedKeyAsync();
        public Task<bool> HasSavedKeyAsync(CancellationToken cancellationToken = default) => keyStore.HasSavedKeyAsync();
    }
}
