using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object représentant la page des rapports et tableaux de bord /Reports.
/// </summary>
public sealed class ReportsPage(IPage page) : BasePage(page)
{
    private ILocator PageTitle => Page.Locator("h1.h3");
    private ILocator DashboardsTab => Page.Locator("a.nav-link[href*='tab=dashboards']");
    private ILocator PivotTab => Page.Locator("a.nav-link[href*='tab=pivot']");

    /// <summary>
    /// Navigue directement vers la page des rapports.
    /// </summary>
    public Task NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        var url = new Uri(new Uri(baseUrl), "/Reports").ToString();
        return Page.GotoAsync(url);
    }

    /// <summary>
    /// Vérifie si la page des rapports est chargée.
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
    /// Clique sur l'onglet du tableau croisé dynamique (TCD).
    /// </summary>
    public Task ClickPivotTabAsync() => PivotTab.ClickAsync();

    /// <summary>
    /// Indique si l'onglet Tableaux de bord est actif.
    /// </summary>
    public async Task<bool> IsDashboardsTabActiveAsync()
    {
        var classAttr = await DashboardsTab.GetAttributeAsync("class");
        return classAttr != null && classAttr.Contains("active");
    }

    /// <summary>
    /// Indique si l'onglet TCD est actif.
    /// </summary>
    public async Task<bool> IsPivotTabActiveAsync()
    {
        var classAttr = await PivotTab.GetAttributeAsync("class");
        return classAttr != null && classAttr.Contains("active");
    }
}
