using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object représentant le tableau de bord des comptes bancaires /Accounts.
/// </summary>
public sealed class DashboardPage(IPage page) : BasePage(page)
{
    private ILocator PageTitle => Page.Locator("h1.h3");
    private ILocator Subtitle => Page.Locator("p.text-muted.small");
    private ILocator NetWorthValue => Page.Locator(".card:has-text('PATRIMOINE') .h3, .card:has-text('NET WORTH') .h3");
    private ILocator LiquidAssetsValue => Page.Locator(".card:has-text('DISPONIBLE') .h3, .card:has-text('LIQUID') .h3");
    private ILocator SavingsValue => Page.Locator(".card:has-text('ÉPARGNE') .h3, .card:has-text('SAVINGS') .h3");
    private ILocator DebtsValue => Page.Locator(".card:has-text('DETTES') .h3, .card:has-text('DEBTS') .h3");
    private ILocator AccountsHeading => Page.Locator("h2.h5");
    private ILocator EmptyAccountsCard => Page.Locator(".card:has-text('Aucun compte bancaire'), .card:has-text('No bank account')");
    private ILocator NewAccountButton => Page.Locator("a.btn-primary[href='/Accounts/CreateAccount']");

    /// <summary>
    /// Navigue directement vers la page des comptes / tableau de bord.
    /// </summary>
    public Task NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        var url = new Uri(new Uri(baseUrl), "/Accounts").ToString();
        return Page.GotoAsync(url);
    }

    /// <summary>
    /// Vérifie si la page du tableau de bord est chargée.
    /// </summary>
    public Task<bool> IsPageLoadedAsync() => PageTitle.IsVisibleAsync();

    /// <summary>
    /// Récupère le titre de la page.
    /// </summary>
    public async Task<string> GetTitleTextAsync()
    {
        return (await PageTitle.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère le texte du sous-titre de la page.
    /// </summary>
    public async Task<string> GetSubtitleTextAsync()
    {
        return (await Subtitle.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère la valeur du patrimoine net affichée sur la carte KPI.
    /// </summary>
    public async Task<string> GetNetWorthValueAsync()
    {
        return (await NetWorthValue.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère la valeur des liquidités disponibles.
    /// </summary>
    public async Task<string> GetLiquidAssetsValueAsync()
    {
        return (await LiquidAssetsValue.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère la valeur de l'épargne.
    /// </summary>
    public async Task<string> GetSavingsValueAsync()
    {
        return (await SavingsValue.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère la valeur des dettes.
    /// </summary>
    public async Task<string> GetDebtsValueAsync()
    {
        return (await DebtsValue.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère le titre de la section des comptes.
    /// </summary>
    public async Task<string> GetAccountsHeadingTextAsync()
    {
        return (await AccountsHeading.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Indique si la carte d'absence de comptes est visible.
    /// </summary>
    public Task<bool> IsEmptyAccountsCardVisibleAsync() => EmptyAccountsCard.IsVisibleAsync();

    /// <summary>
    /// Clique sur le bouton de création d'un nouveau compte bancaire.
    /// </summary>
    public Task ClickNewAccountButtonAsync() => NewAccountButton.First.ClickAsync();
}
