using AwesomeAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;
using Microsoft.AspNetCore.Http;
using static Microsoft.Playwright.Assertions;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Test E2E de chargement de la page EditAccount.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class EditAccountPageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : AuthenticatedE2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task EditAccountPage_WhenLoaded_DisplaysMainContent()
    {
        // Arrange
        await RegisterAndSignInAsync();
        var accountId = await CreateAccountAndGetIdAsync();
        var page = new EditAccountPage(Page);

        // Act
        var response = await page.NavigateAsync(BaseUrl, accountId);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(StatusCodes.Status200OK);
        await Expect(page.MainHeading).ToBeVisibleAsync();
    }
}
