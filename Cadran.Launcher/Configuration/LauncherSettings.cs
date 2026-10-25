using System.Globalization;

namespace Cadran.Launcher.Configuration;

/// <summary>
/// Paramètres de configuration du superviseur Cadran et de l'interface graphique Windows.
/// </summary>
/// <param name="Port">Numéro de port local sur lequel Kestrel écoute.</param>
/// <param name="Language">Code de langue de l'interface (ex: 'fr', 'en', 'es', 'de'). Si null, détecté automatiquement.</param>
/// <param name="AutoStartServer">Indique si le serveur doit démarrer automatiquement à l'ouverture du superviseur.</param>
/// <param name="AutoOpenBrowser">Indique si le navigateur doit être ouvert dès que le serveur est prêt.</param>
/// <param name="StartMinimizedInTray">Indique si la fenêtre doit être réduite dans la zone de notification au lancement.</param>
/// <param name="EnableAutomaticBackups">Indique si les sauvegardes automatiques au lancement et à la fermeture sont activées.</param>
/// <param name="RetentionPolicy">Politique de rétention et de rotation des fichiers de sauvegarde.</param>
public sealed record LauncherSettings(
    int Port = 5000,
    string? Language = null,
    bool AutoStartServer = true,
    bool AutoOpenBrowser = true,
    bool StartMinimizedInTray = false,
    bool EnableAutomaticBackups = true,
    BackupRetentionOptions? RetentionPolicy = null)
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "fr", "en", "es", "de"
    };

    /// <summary>
    /// Obtient la politique de rétention effective, garantie non nulle.
    /// </summary>
    public BackupRetentionOptions RetentionPolicy { get; init; } = RetentionPolicy ?? new BackupRetentionOptions();

    /// <summary>
    /// Valide et normalise les valeurs des paramètres.
    /// </summary>
    /// <returns>Une instance normalisée.</returns>
    public LauncherSettings Normalize()
    {
        var sanitizedPort = Port is >= 1024 and <= 65535 ? Port : 5000;
        var sanitizedLang = SanitizeLanguage(Language);

        return new LauncherSettings(
            sanitizedPort,
            sanitizedLang,
            AutoStartServer,
            AutoOpenBrowser,
            StartMinimizedInTray,
            EnableAutomaticBackups,
            RetentionPolicy.Normalize());
    }

    private static string SanitizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            var uiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            return SupportedLanguages.Contains(uiLang) ? uiLang : "fr";
        }

        var trimmed = language.Trim().ToLowerInvariant();
        if (trimmed.Length >= 2)
        {
            var prefix = trimmed[..2];
            if (SupportedLanguages.Contains(prefix))
            {
                return prefix;
            }
        }

        return "fr";
    }
}
