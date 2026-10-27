using AwesomeAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Tests E2E Playwright pour la page de gestion des catégories.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class CategoriesPageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task CategoriesPage_WhenLoadedByAuthenticatedUser_DisplaysTitleAndActionButtons()
    {
        // Arrange
        var registerPage = new RegisterPage(Page);
        var categoriesPage = new CategoriesPage(Page);

        await registerPage.NavigateAsync(BaseUrl);
        var uniqueEmail = $"cat_{Guid.NewGuid():N}@Ledger.local";
        await registerPage.RegisterAsync("Claire Martin", uniqueEmail, "Password123", "Password123");
        await Page.WaitForURLAsync("**/Accounts*");

        // Act
        await categoriesPage.NavigateAsync(BaseUrl);
        var isLoaded = await categoriesPage.IsPageLoadedAsync();
        var title = await categoriesPage.GetTitleTextAsync();
        var isToggleVisible = await categoriesPage.IsToggleAllButtonVisibleAsync();

        // Assert
        isLoaded.Should().BeTrue();
        title.Should().NotBeNullOrWhiteSpace();
        isToggleVisible.Should().BeTrue();
    }

    [Fact]
    public async Task NewCategoryButton_WhenClicked_NavigatesToCreateOrEditPage()
    {
        // Arrange
        var registerPage = new RegisterPage(Page);
        var categoriesPage = new CategoriesPage(Page);

        await registerPage.NavigateAsync(BaseUrl);
        var uniqueEmail = $"newcat_{Guid.NewGuid():N}@Ledger.local";
        await registerPage.RegisterAsync("David Bernard", uniqueEmail, "Password123", "Password123");
        await Page.WaitForURLAsync("**/Accounts*");

        await categoriesPage.NavigateAsync(BaseUrl);

        // Act
        await categoriesPage.ClickNewCategoryButtonAsync();
        await Page.WaitForURLAsync("**/Categories/CreateOrEdit*");

        // Assert
        Page.Url.Should().Contain("/Categories/CreateOrEdit");
    }
}
