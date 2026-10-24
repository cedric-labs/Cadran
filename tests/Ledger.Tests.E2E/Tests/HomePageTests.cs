using FluentAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Tests E2E Playwright validant l'affichage, l'interactivité et l'intégrité de la page d'accueil.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class HomePageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task HomePage_WhenVisited_DisplaysHeroTitleAndFeatureCards()
    {
        var homePage = new HomePage(Page);
        await homePage.NavigateAsync(BaseUrl);

        var heading = await homePage.GetHeroHeadingTextAsync();
        var areFeaturesVisible = await homePage.AreFeatureCardsVisibleAsync();

        heading.Should().NotBeNullOrWhiteSpace();
        areFeaturesVisible.Should().BeTrue();
    }

    [Fact]
    public async Task HomePage_WhenGetStartedButtonClicked_NavigatesToRegisterPage()
    {
        var homePage = new HomePage(Page);
        var registerPage = new RegisterPage(Page);
        await homePage.NavigateAsync(BaseUrl);

        await homePage.ClickGetStartedButtonAsync();
        await Page.WaitForURLAsync("**/Security/Register*");

        var isFormVisible = await registerPage.IsFormVisibleAsync();
        isFormVisible.Should().BeTrue();
    }

    [Fact]
    public async Task HomePage_WhenLoginButtonClicked_NavigatesToLoginPage()
    {
        var homePage = new HomePage(Page);
        var loginPage = new LoginPage(Page);
        await homePage.NavigateAsync(BaseUrl);

        await homePage.ClickHeroLoginButtonAsync();
        await Page.WaitForURLAsync("**/Security/Login*");

        var isFormVisible = await loginPage.IsFormVisibleAsync();
        isFormVisible.Should().BeTrue();
    }

    [Fact]
    public async Task HomePage_WhenLoaded_DisplaysAccurateFeatureData()
    {
        var homePage = new HomePage(Page);
        await homePage.NavigateAsync(BaseUrl);

        var titles = await homePage.GetFeatureCardTitlesAsync();
        titles.Should().HaveCountGreaterThanOrEqualTo(3);
        titles.Should().Contain(t => t.Contains("Règles", StringComparison.OrdinalIgnoreCase) || t.Contains("Rules", StringComparison.OrdinalIgnoreCase));
        titles.Should().Contain(t => t.Contains("Sécurité", StringComparison.OrdinalIgnoreCase) || t.Contains("Security", StringComparison.OrdinalIgnoreCase));
        titles.Should().Contain(t => t.Contains("Imports", StringComparison.OrdinalIgnoreCase) || t.Contains("Import", StringComparison.OrdinalIgnoreCase));
    }
}
