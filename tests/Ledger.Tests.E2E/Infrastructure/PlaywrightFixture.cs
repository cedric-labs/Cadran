using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Infrastructure;

/// <summary>
/// Fixture partagée gérant le cycle de vie de l'instance Playwright et du navigateur Chromium.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>
    /// Initialise le moteur Playwright et démarre le navigateur Chromium en mode headless.
    /// </summary>
    public async Task InitializeAsync()
    {
        PlaywrightBrowserInstaller.EnsureChromiumInstalled();

        _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Crée une nouvelle page dans un contexte de navigation isolé avec des dimensions standardisées.
    /// </summary>
    public async Task<IPage> CreatePageAsync()
    {
        if (_browser is null)
        {
            throw new InvalidOperationException("Le navigateur Playwright n'est pas initialisé.");
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
            IgnoreHTTPSErrors = true
        }).ConfigureAwait(false);

        return await context.NewPageAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Libère les ressources du navigateur et du pilote Playwright.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
    }
}
