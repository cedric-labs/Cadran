namespace Ledger.Tests.E2E.Infrastructure;

/// <summary>
/// Assure l'installation et la disponibilité automatique des navigateurs Playwright requis pour l'exécution des tests E2E.
/// </summary>
public static class PlaywrightBrowserInstaller
{
    private static readonly Lock SyncLock = new();
    private static bool _isInstalled;

    /// <summary>
    /// Télécharge et installe automatiquement le navigateur Chromium s'il n'est pas déjà présent.
    /// </summary>
    public static void EnsureChromiumInstalled()
    {
        if (_isInstalled)
        {
            return;
        }

        lock (SyncLock)
        {
            if (_isInstalled)
            {
                return;
            }

            var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Échec de l'installation automatique du navigateur Chromium (Code : {exitCode}).");
            }

            _isInstalled = true;
        }
    }
}
