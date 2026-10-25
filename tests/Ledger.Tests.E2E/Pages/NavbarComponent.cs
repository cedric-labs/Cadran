using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object représentant la barre de navigation principale de l'application.
/// </summary>
public sealed class NavbarComponent(IPage page) : BasePage(page)
{
    private ILocator BrandLink => Page.Locator("a.navbar-brand");
    private ILocator LoginLink => Page.Locator("a.nav-link[href='/Security/Login']");
    private ILocator RegisterButton => Page.Locator("a.btn-primary[href='/Security/Register']");
    private ILocator AccountsLink => Page.Locator("a.nav-link[href='/Accounts']");
    private ILocator TransactionsLink => Page.Locator("a.nav-link[href='/Transactions']");
    private ILocator CategoriesLink => Page.Locator("a.nav-link[href='/Categories']");
    private ILocator LanguageDropdownToggle => Page.Locator("button.dropdown-toggle:has(i.bi-translate)");
    private ILocator LanguageBadge => Page.Locator("button.dropdown-toggle span.small");
    private ILocator UserDropdownToggle => Page.Locator("a.dropdown-toggle:has(i.bi-person-fill)");
    private ILocator LogoutButton => Page.Locator("form[action='/Security/Logout'] button[type='submit']");

    /// <summary>
    /// Vérifie la visibilité du logo / titre de marque dans la barre de navigation.
    /// </summary>
    public Task<bool> IsBrandVisibleAsync() => BrandLink.IsVisibleAsync();

    /// <summary>
    /// Récupère le texte de la marque.
    /// </summary>
    public async Task<string> GetBrandTextAsync()
    {
        return (await BrandLink.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Clique sur le logo / marque de l'application.
    /// </summary>
    public Task ClickBrandAsync() => BrandLink.ClickAsync();

    /// <summary>
    /// Clique sur le lien d'accès à la page de connexion.
    /// </summary>
    public Task ClickLoginAsync() => LoginLink.ClickAsync();

    /// <summary>
    /// Clique sur le bouton d'inscription.
    /// </summary>
    public Task ClickRegisterAsync() => RegisterButton.ClickAsync();

    /// <summary>
    /// Clique sur le lien des comptes bancaires.
    /// </summary>
    public Task ClickAccountsAsync() => AccountsLink.ClickAsync();

    /// <summary>
    /// Récupère le code de langue actuellement sélectionné affiché sur le badge du menu.
    /// </summary>
    public async Task<string> GetCurrentLanguageCodeAsync()
    {
        return (await LanguageBadge.InnerTextAsync()).Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Déroule le menu de langue et sélectionne une culture cible.
    /// </summary>
    public async Task SelectLanguageAsync(string cultureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureCode);
        await LanguageDropdownToggle.ClickAsync();
        var targetLink = Page.Locator($"a.dropdown-item[href*='culture={cultureCode}']");
        await targetLink.ClickAsync();
    }

    /// <summary>
    /// Indique si le menu de l'utilisateur connecté est visible.
    /// </summary>
    public Task<bool> IsUserMenuVisibleAsync() => UserDropdownToggle.IsVisibleAsync();

    /// <summary>
    /// Récupère le nom affiché de l'utilisateur connecté.
    /// </summary>
    public async Task<string> GetLoggedInUserNameAsync()
    {
        return (await UserDropdownToggle.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Déroule le menu utilisateur et clique sur le bouton de déconnexion.
    /// </summary>
    public async Task ClickLogoutAsync()
    {
        await UserDropdownToggle.ClickAsync();
        await LogoutButton.ClickAsync();
    }
}
