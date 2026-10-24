using FluentAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Tests E2E Playwright pour le tableau de bord des comptes bancaires.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class DashboardPageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task DashboardPage_WhenAuthenticatedUserAccessesAccounts_DisplaysKpiCardsAndEmptyState()
    {
        var registerPage = new RegisterPage(Page);
        var dashboardPage = new DashboardPage(Page);

        await registerPage.NavigateAsync(BaseUrl);
        var uniqueEmail = $"dash_{Guid.NewGuid():N}@Ledger.local";
        await registerPage.RegisterAsync("Alice Martin", uniqueEmail, "Password123", "Password123");

        await Page.WaitForURLAsync("**/Accounts*");

        var isLoaded = await dashboardPage.IsPageLoadedAsync();
        var heading = await dashboardPage.GetAccountsHeadingTextAsync();
        var isEmptyVisible = await dashboardPage.IsEmptyAccountsCardVisibleAsync();

        isLoaded.Should().BeTrue();
        heading.Should().Contain("(0)");
        isEmptyVisible.Should().BeTrue();
    }

    [Fact]
    public async Task DashboardPage_WhenLoaded_DisplaysAccurateInitialZeroBalances()
    {
        var registerPage = new RegisterPage(Page);
        var dashboardPage = new DashboardPage(Page);

        await registerPage.NavigateAsync(BaseUrl);
        var uniqueEmail = $"kpi_{Guid.NewGuid():N}@Ledger.local";
        await registerPage.RegisterAsync("Bob Dupont", uniqueEmail, "Password123", "Password123");

        await Page.WaitForURLAsync("**/Accounts*");

        var netWorth = await dashboardPage.GetNetWorthValueAsync();
        var liquid = await dashboardPage.GetLiquidAssetsValueAsync();

        netWorth.Should().Contain("0,00");
        liquid.Should().Contain("0,00");
    }
}
