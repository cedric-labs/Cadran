using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object représentant la page d'accueil publique de l'application.
/// </summary>
public sealed class HomePage(IPage page) : BasePage(page)
{
    private ILocator HeroBadge => Page.Locator("span.badge.bg-primary-subtle");
    private ILocator HeroHeading => Page.Locator("h1.display-4");
    private ILocator HeroLead => Page.Locator("p.lead");
    private ILocator GetStartedButton => Page.Locator("main a.btn-lg[href='/Security/Register']");
    private ILocator LoginButton => Page.Locator("main a.btn-outline-secondary[href='/Security/Login']");
    private ILocator FeatureCards => Page.Locator(".card h3");

    /// <summary>
    /// Navigue vers la page d'accueil à partir de l'URL de base spécifiée.
    /// </summary>
    public Task NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return Page.GotoAsync(baseUrl);
    }

    /// <summary>
    /// Récupère le texte du badge d'en-tête de la page d'accueil.
    /// </summary>
    public async Task<string> GetHeroBadgeTextAsync()
    {
        return (await HeroBadge.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère le titre principal de la page d'accueil.
    /// </summary>
    public async Task<string> GetHeroHeadingTextAsync()
    {
        return (await HeroHeading.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère le paragraphe d'introduction sous le titre.
    /// </summary>
    public async Task<string> GetHeroLeadTextAsync()
    {
        return (await HeroLead.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Vérifie que les cartes de présentation des fonctionnalités sont visibles.
    /// </summary>
    public async Task<bool> AreFeatureCardsVisibleAsync()
    {
        var count = await FeatureCards.CountAsync();
        return count >= 3;
    }

    /// <summary>
    /// Récupère la liste des titres des fonctionnalités présentées.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetFeatureCardTitlesAsync()
    {
        var count = await FeatureCards.CountAsync();
        var titles = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            var text = (await FeatureCards.Nth(i).InnerTextAsync()).Trim();
            titles.Add(text);
        }
        return titles;
    }

    /// <summary>
    /// Clique sur le bouton d'inscription "Créer un compte".
    /// </summary>
    public Task ClickGetStartedButtonAsync() => GetStartedButton.ClickAsync();

    /// <summary>
    /// Clique sur le bouton de connexion présent dans la section d'introduction.
    /// </summary>
    public Task ClickHeroLoginButtonAsync() => LoginButton.ClickAsync();
}
