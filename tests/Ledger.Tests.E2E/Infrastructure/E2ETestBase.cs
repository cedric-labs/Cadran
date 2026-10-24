using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Infrastructure;

/// <summary>
/// Classe de base pour les tests E2E Playwright fournissant l'accès à la page et aux fixtures.
/// </summary>
[Collection(E2ETestCollection.Name)]
public abstract class E2ETestBase(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture) : IAsyncLifetime
{
    private IPage? _page;

    /// <summary>
    /// Fixture du serveur Kestrel.
    /// </summary>
    protected AppWebTestFixture WebFixture { get; } = webFixture;

    /// <summary>
    /// Fixture du navigateur Playwright.
    /// </summary>
    protected PlaywrightFixture PlaywrightFixture { get; } = playwrightFixture;

    /// <summary>
    /// Page Playwright active pour le test en cours.
    /// </summary>
    protected IPage Page => _page ?? throw new InvalidOperationException("La page de test n'est pas initialisée.");

    /// <summary>
    /// URL racine du serveur Kestrel en écoute.
    /// </summary>
    protected string BaseUrl => WebFixture.ServerAddress;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _page = await PlaywrightFixture.CreatePageAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_page is not null)
        {
            var context = _page.Context;
            await _page.CloseAsync();
            if (context is not null)
            {
                await context.CloseAsync();
            }
            _page = null;
        }
    }
}
