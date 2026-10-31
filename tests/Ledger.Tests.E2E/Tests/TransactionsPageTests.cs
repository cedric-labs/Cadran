using AwesomeAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;
using Microsoft.AspNetCore.Http;
using static Microsoft.Playwright.Assertions;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Test E2E de chargement de la page Transactions.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class TransactionsPageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : AuthenticatedE2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task TransactionsPage_WhenLoaded_DisplaysMainContent()
    {
        // Arrange
        await RegisterAndSignInAsync();
        var page = new TransactionsPage(Page);

        // Act
        var response = await page.NavigateAsync(BaseUrl);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(StatusCodes.Status200OK);
        await Expect(page.MainHeading).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ManualEntryModal_WhenDebitIsSubmitted_ShowsOperationWithManualBadge()
    {
        // Arrange
        await RegisterAndSignInAsync();
        await CreateAccountAndGetIdAsync();
        var page = new TransactionsPage(Page);
        await page.NavigateAsync(BaseUrl);

        // Act
        await page.CreateManualDebitAsync("Boulangerie E2E", "12,50");

        // Assert
        await Expect(page.ManualEntryResult).ToContainTextAsync("L'opération a été ajoutée.");
        await Expect(page.ManualOriginBadges).ToHaveCountAsync(1);
        await Expect(page.ManualOriginBadges.First).ToContainTextAsync("Saisie manuelle");
        await Expect(Page.GetByText("Boulangerie E2E")).ToBeVisibleAsync();
    }
}
