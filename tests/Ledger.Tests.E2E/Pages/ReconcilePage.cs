using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object de la page /Accounts/Reconcile.
/// </summary>
public sealed class ReconcilePage(IPage page) : BasePage(page)
{
    /// <summary>Titre principal de la page.</summary>
    public ILocator MainHeading => Page.GetByRole(AriaRole.Heading, new() { Level = 1 });

    /// <summary>Lignes de paires proposées à la fusion.</summary>
    public ILocator PairRows => Page.Locator("[data-testid^='reconcile-row-']");

    /// <summary>Boutons de fusion, un par paire.</summary>
    public ILocator MergeButtons => Page.Locator("[data-testid^='reconcile-merge-']");

    /// <summary>Message de résultat affiché après une fusion.</summary>
    public ILocator Result => Page.GetByTestId("reconcile-result");

    /// <summary>Message affiché quand aucune paire n'est proposée.</summary>
    public ILocator EmptyState => Page.GetByTestId("reconcile-empty");

    /// <summary>
    /// Navigue vers la page et renvoie la réponse HTTP du serveur.
    /// </summary>
    public Task<IResponse?> NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return Page.GotoAsync(new Uri(new Uri(baseUrl), "/Accounts/Reconcile").ToString());
    }
}
