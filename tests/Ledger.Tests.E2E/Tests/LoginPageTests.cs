using FluentAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Tests E2E Playwright pour la page d'authentification.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class LoginPageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task LoginPage_WhenLoaded_DisplaysAllRequiredFormElements()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(BaseUrl);

        var isVisible = await loginPage.IsFormVisibleAsync();
        var heading = await loginPage.GetHeadingTextAsync();

        isVisible.Should().BeTrue();
        heading.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoginPage_WhenSubmittedEmpty_DisplaysValidationErrors()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(BaseUrl);

        await loginPage.SubmitAsync();

        var emailError = await loginPage.GetEmailValidationErrorAsync();
        var passwordError = await loginPage.GetPasswordValidationErrorAsync();

        emailError.Should().NotBeNullOrWhiteSpace();
        passwordError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoginPage_WhenSubmittedInvalidCredentials_DisplaysErrorMessage()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(BaseUrl);

        await loginPage.LoginAsync("inconnu@Ledger.local", "MotDePasseInvalide123");

        var summaryText = await loginPage.GetValidationSummaryTextAsync();
        summaryText.Should().Contain("Identifiants invalides");
    }

    [Fact]
    public async Task LoginPage_WhenCreateAccountClicked_NavigatesToRegisterPage()
    {
        var loginPage = new LoginPage(Page);
        var registerPage = new RegisterPage(Page);
        await loginPage.NavigateAsync(BaseUrl);

        await loginPage.ClickRegisterLinkAsync();
        await Page.WaitForURLAsync("**/Security/Register*");

        var isRegisterVisible = await registerPage.IsFormVisibleAsync();
        isRegisterVisible.Should().BeTrue();
    }
}
