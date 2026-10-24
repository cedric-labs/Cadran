using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object représentant la page de gestion des catégories /Categories.
/// </summary>
public sealed class CategoriesPage(IPage page) : BasePage(page)
{
    private ILocator PageTitle => Page.Locator("h1.h3");
    private ILocator Subtitle => Page.Locator("p.text-muted.small").First;
    private ILocator ToggleAllButton => Page.Locator("#btnToggleAllCategories");
    private ILocator NewCategoryButton => Page.Locator("a.btn-primary[href='/Categories/CreateOrEdit']").First;

    /// <summary>
    /// Navigue directement vers la page des catégories.
    /// </summary>
    public Task NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        var url = new Uri(new Uri(baseUrl), "/Categories").ToString();
        return Page.GotoAsync(url);
    }

    /// <summary>
    /// Vérifie si la page des catégories est chargée.
    /// </summary>
    public Task<bool> IsPageLoadedAsync() => PageTitle.IsVisibleAsync();

    /// <summary>
    /// Récupère le titre principal de la page.
    /// </summary>
    public async Task<string> GetTitleTextAsync()
    {
        return (await PageTitle.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère le sous-titre de la page.
    /// </summary>
    public async Task<string> GetSubtitleTextAsync()
    {
        return (await Subtitle.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Clique sur le bouton de création d'une nouvelle catégorie.
    /// </summary>
    public Task ClickNewCategoryButtonAsync() => NewCategoryButton.ClickAsync();

    /// <summary>
    /// Indique si le bouton de bascule de toutes les catégories est affiché.
    /// </summary>
    public Task<bool> IsToggleAllButtonVisibleAsync() => ToggleAllButton.IsVisibleAsync();
}
