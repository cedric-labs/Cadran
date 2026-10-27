using AwesomeAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Tests E2E Playwright pour le module de rapports et tableaux de bord.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class ReportsPageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task ReportsPage_WhenLoadedByAuthenticatedUser_DisplaysTitleAndDashboardsTab()
    {
        // Arrange
        var registerPage = new RegisterPage(Page);
        var reportsPage = new ReportsPage(Page);

        await registerPage.NavigateAsync(BaseUrl);
        var uniqueEmail = $"rep_{Guid.NewGuid():N}@Ledger.local";
        await registerPage.RegisterAsync("Emma Leroy", uniqueEmail, "Password123", "Password123");
        await Page.WaitForURLAsync("**/Accounts*");

        // Act
        await reportsPage.NavigateAsync(BaseUrl);
        var isLoaded = await reportsPage.IsPageLoadedAsync();
        var title = await reportsPage.GetTitleTextAsync();
        var isDashboardsActive = await reportsPage.IsDashboardsTabActiveAsync();

        // Assert
        isLoaded.Should().BeTrue();
        title.Should().NotBeNullOrWhiteSpace();
        isDashboardsActive.Should().BeTrue();
    }

    [Fact]
    public async Task PivotTab_WhenClicked_NavigatesToPivotTabAndActivatesView()
    {
        // Arrange
        var registerPage = new RegisterPage(Page);
        var reportsPage = new ReportsPage(Page);

        await registerPage.NavigateAsync(BaseUrl);
        var uniqueEmail = $"pivot_{Guid.NewGuid():N}@Ledger.local";
        await registerPage.RegisterAsync("Felix Roux", uniqueEmail, "Password123", "Password123");
        await Page.WaitForURLAsync("**/Accounts*");

        await reportsPage.NavigateAsync(BaseUrl);

        // Act
        await reportsPage.ClickPivotTabAsync();
        await Page.WaitForURLAsync("**/Reports*tab=pivot*");

        // Assert
        Page.Url.Should().Contain("tab=pivot");
        var isPivotActive = await reportsPage.IsPivotTabActiveAsync();
        isPivotActive.Should().BeTrue();
    }
}
