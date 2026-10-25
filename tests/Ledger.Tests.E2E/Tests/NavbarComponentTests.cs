using FluentAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Tests E2E Playwright pour le composant de barre de navigation.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class NavbarComponentTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task NavbarComponent_WhenLoaded_DisplaysBrandAndPublicLinks()
    {
        var navbar = new NavbarComponent(Page);
        await Page.GotoAsync(BaseUrl);

        var isBrandVisible = await navbar.IsBrandVisibleAsync();
        var brandText = await navbar.GetBrandTextAsync();

        isBrandVisible.Should().BeTrue();
        brandText.Should().Contain("Cadran");
    }

    [Fact]
    public async Task NavbarComponent_WhenBrandClicked_NavigatesToExpectedRoute()
    {
        var navbar = new NavbarComponent(Page);
        await Page.GotoAsync(BaseUrl);

        await navbar.ClickBrandAsync();

        // Si l'utilisateur n'est pas connecté, l'accès à /Accounts redirige vers /Security/Login
        await Page.WaitForURLAsync("**/Security/Login*");
        Page.Url.Should().Contain("/Security/Login");
    }

    [Fact]
    public async Task NavbarComponent_WhenLanguageChangedToEnglish_UpdatesInterfaceToEnglish()
    {
        var navbar = new NavbarComponent(Page);
        var homePage = new HomePage(Page);
        await homePage.NavigateAsync(BaseUrl);

        await navbar.SelectLanguageAsync("en");

        var currentLang = await navbar.GetCurrentLanguageCodeAsync();
        currentLang.Should().Be("en");
    }

    [Fact]
    public async Task NavbarComponent_WhenLanguageChangedToSpanish_UpdatesInterfaceToSpanish()
    {
        var navbar = new NavbarComponent(Page);
        var homePage = new HomePage(Page);
        await homePage.NavigateAsync(BaseUrl);

        await navbar.SelectLanguageAsync("es");

        var currentLang = await navbar.GetCurrentLanguageCodeAsync();
        currentLang.Should().Be("es");
    }

    [Fact]
    public async Task NavbarComponent_WhenLanguageChangedToGerman_UpdatesInterfaceToGerman()
    {
        var navbar = new NavbarComponent(Page);
        var homePage = new HomePage(Page);
        await homePage.NavigateAsync(BaseUrl);

        await navbar.SelectLanguageAsync("de");

        var currentLang = await navbar.GetCurrentLanguageCodeAsync();
        currentLang.Should().Be("de");
    }
}
