using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object de la page /Error.
/// </summary>
public sealed class ErrorPage(IPage page) : BasePage(page)
{
    /// <summary>
    /// Titre principal de la page.
    /// </summary>
    public ILocator MainHeading => Page.GetByRole(AriaRole.Heading, new() { Level = 1 });

    /// <summary>
    /// Navigue vers la page et renvoie la réponse HTTP du serveur.
    /// </summary>
    public Task<IResponse?> NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return Page.GotoAsync(new Uri(new Uri(baseUrl), "/Error").ToString());
    }
}
