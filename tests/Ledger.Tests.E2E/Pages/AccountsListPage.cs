using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object de la liste des comptes /Accounts, utilisé pour retrouver l'identifiant d'un compte créé.
/// </summary>
public sealed class AccountsListPage(IPage page) : BasePage(page)
{
    private ILocator FirstDetailsLink => Page.Locator("a[href^='/Accounts/Details/']").First;

    /// <summary>
    /// Navigue vers la liste des comptes.
    /// </summary>
    public Task<IResponse?> NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return Page.GotoAsync(new Uri(new Uri(baseUrl), "/Accounts").ToString());
    }

    /// <summary>
    /// Lit l'identifiant du premier compte listé dans l'URL de son lien de détail.
    /// </summary>
    public async Task<Guid> GetFirstAccountIdAsync()
    {
        var href = await FirstDetailsLink.GetAttributeAsync("href");
        var idSegment = href?.Split('/', StringSplitOptions.RemoveEmptyEntries)[^1]
            ?? throw new InvalidOperationException("Aucun lien de détail de compte n'est affiché dans la liste.");
        return Guid.Parse(idSegment);
    }
}
