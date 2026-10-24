using FluentAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Tests E2E Playwright pour la page d'inscription.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class RegisterPageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task RegisterPage_WhenLoaded_DisplaysAllRequiredFormElements()
    {
        var registerPage = new RegisterPage(Page);
        await registerPage.NavigateAsync(BaseUrl);

        var isVisible = await registerPage.IsFormVisibleAsync();
        var heading = await registerPage.GetHeadingTextAsync();

        isVisible.Should().BeTrue();
        heading.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RegisterPage_WhenSubmittedWithEmptyFields_DisplaysValidationErrors()
    {
        var registerPage = new RegisterPage(Page);
        await registerPage.NavigateAsync(BaseUrl);

        await registerPage.SubmitAsync();

        var nameError = await registerPage.GetFullNameValidationErrorAsync();
        var emailError = await registerPage.GetEmailValidationErrorAsync();
        var passwordError = await registerPage.GetPasswordValidationErrorAsync();

        nameError.Should().NotBeNullOrWhiteSpace();
        emailError.Should().NotBeNullOrWhiteSpace();
        passwordError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RegisterPage_WhenPasswordsDoNotMatch_DisplaysMismatchError()
    {
        var registerPage = new RegisterPage(Page);
        await registerPage.NavigateAsync(BaseUrl);

        await registerPage.RegisterAsync("Jean Dupont", "jean@Ledger.local", "Password123", "MismatchPassword");

        var confirmError = await registerPage.GetConfirmPasswordValidationErrorAsync();
        confirmError.Should().Contain("ne correspondent pas");
    }

    [Fact]
    public async Task RegisterPage_WhenValidUserRegistered_NavigatesToDashboardAndAuthenticates()
    {
        var registerPage = new RegisterPage(Page);
        var dashboardPage = new DashboardPage(Page);
        var navbar = new NavbarComponent(Page);

        await registerPage.NavigateAsync(BaseUrl);

        var uniqueEmail = $"user_{Guid.NewGuid():N}@Ledger.local";
        await registerPage.RegisterAsync("Utilisateur E2E", uniqueEmail, "Password123", "Password123");

        await Page.WaitForURLAsync("**/Accounts*");

        var isDashboardLoaded = await dashboardPage.IsPageLoadedAsync();
        isDashboardLoaded.Should().BeTrue();

        var isUserMenuVisible = await navbar.IsUserMenuVisibleAsync();
        isUserMenuVisible.Should().BeTrue();
    }
}
