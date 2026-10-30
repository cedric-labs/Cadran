using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object de la page /Security/UnlockDatabase.
/// </summary>
public sealed class UnlockDatabasePage(IPage page) : BasePage(page)
{
    /// <summary>
    /// Titre principal de la page.
    /// </summary>
    public ILocator MainHeading => Page.GetByRole(AriaRole.Heading, new() { Level = 2 });

    /// <summary>
    /// Champ de saisie de la phrase de passe maître.
    /// </summary>
    public ILocator PassphraseInput => Page.Locator("#dbPassphraseInput");

    /// <summary>
    /// Navigue vers la page et renvoie la réponse HTTP du serveur.
    /// </summary>
    public Task<IResponse?> NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return Page.GotoAsync(new Uri(new Uri(baseUrl), "/Security/UnlockDatabase").ToString());
    }
}
