using FluentAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Tests E2E Playwright pour le formulaire de création de compte bancaire.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class CreateAccountPageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task CreateAccountPage_WhenLoadedByAuthenticatedUser_DisplaysAllRequiredFormFields()
    {
        // Arrange
        var registerPage = new RegisterPage(Page);
        var createAccountPage = new CreateAccountPage(Page);

        await registerPage.NavigateAsync(BaseUrl);
        var uniqueEmail = $"acc_{Guid.NewGuid():N}@Ledger.local";
        await registerPage.RegisterAsync("Gabriel Petit", uniqueEmail, "Password123", "Password123");
        await Page.WaitForURLAsync("**/Accounts*");

        // Act
        await createAccountPage.NavigateAsync(BaseUrl);
        var isVisible = await createAccountPage.IsFormVisibleAsync();
        var heading = await createAccountPage.GetHeadingTextAsync();

        // Assert
        isVisible.Should().BeTrue();
        heading.Should().Contain("Nouveau compte");
    }

    [Fact]
    public async Task CreateAccountPage_WhenSubmittedEmpty_DisplaysValidationErrors()
    {
        // Arrange
        var registerPage = new RegisterPage(Page);
        var createAccountPage = new CreateAccountPage(Page);

        await registerPage.NavigateAsync(BaseUrl);
        var uniqueEmail = $"accval_{Guid.NewGuid():N}@Ledger.local";
        await registerPage.RegisterAsync("Hugo Moreau", uniqueEmail, "Password123", "Password123");
        await Page.WaitForURLAsync("**/Accounts*");

        await createAccountPage.NavigateAsync(BaseUrl);

        // Act
        await createAccountPage.SubmitAsync();

        // Assert
        var error = await createAccountPage.GetAccountNameErrorAsync();
        error.Should().NotBeNullOrWhiteSpace();
    }
}
