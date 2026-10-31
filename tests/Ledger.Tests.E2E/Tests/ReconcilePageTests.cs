using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Infrastructure.Data;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Microsoft.Playwright.Assertions;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Tests E2E de l'écran de rapprochement des saisies manuelles.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class ReconcilePageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : AuthenticatedE2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task ReconcilePage_WhenLoadedWithoutManualEntry_DisplaysEmptyState()
    {
        // Arrange
        await RegisterAndSignInAsync();
        var page = new ReconcilePage(Page);

        // Act
        var response = await page.NavigateAsync(BaseUrl);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(StatusCodes.Status200OK);
        await Expect(page.MainHeading).ToBeVisibleAsync();
        await Expect(page.EmptyState).ToBeVisibleAsync();
    }

    [Fact]
    public async Task MergeButton_WhenClicked_MergesTheManualEntryIntoTheImportedTransaction()
    {
        // Arrange
        await RegisterAndSignInAsync();
        var accountId = await CreateAccountAndGetIdAsync();
        var transactions = new TransactionsPage(Page);
        await transactions.NavigateAsync(BaseUrl);
        await transactions.CreateManualDebitAsync("Boulangerie E2E", "12,50");
        await Expect(transactions.ManualOriginBadges).ToHaveCountAsync(1);
        await SeedImportedDebitAsync(accountId, "CB BOULANGERIE E2E", 12.50m);
        var page = new ReconcilePage(Page);
        await page.NavigateAsync(BaseUrl);
        await Expect(page.PairRows).ToHaveCountAsync(1);

        // Act
        await page.MergeButtons.First.ClickAsync();

        // Assert
        await Expect(page.Result).ToContainTextAsync("a été fusionnée");
        await Expect(page.EmptyState).ToBeVisibleAsync();
        await transactions.NavigateAsync(BaseUrl);
        await Expect(transactions.ManualOriginBadges).ToHaveCountAsync(0);
        await Expect(Page.GetByText("CB BOULANGERIE E2E")).ToBeVisibleAsync();
    }

    private async Task SeedImportedDebitAsync(Guid accountId, string label, decimal amount)
    {
        using var scope = WebFixture.KestrelServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.BankAccounts.IgnoreQueryFilters().SingleAsync(a => a.Id == accountId);
        db.Transactions.Add(new Transaction
        {
            UserId = account.UserId,
            BankAccountId = accountId,
            Date = DateTime.Today,
            Amount = amount,
            Type = TransactionType.Debit,
            Label = label,
            Origin = TransactionOrigin.Import,
        });
        await db.SaveChangesAsync();
    }
}
