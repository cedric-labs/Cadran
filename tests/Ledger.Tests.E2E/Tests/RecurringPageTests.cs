using AwesomeAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;
using Microsoft.AspNetCore.Http;
using static Microsoft.Playwright.Assertions;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Test E2E de chargement de la page Recurring.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class RecurringPageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : AuthenticatedE2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task RecurringPage_WhenLoaded_DisplaysMainContent()
    {
        // Arrange
        await RegisterAndSignInAsync();
        var page = new RecurringPage(Page);

        // Act
        var response = await page.NavigateAsync(BaseUrl);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(StatusCodes.Status200OK);
        await Expect(page.MainHeading).ToBeVisibleAsync();
    }
}
