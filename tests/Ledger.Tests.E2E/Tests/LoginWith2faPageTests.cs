using AwesomeAssertions;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;
using static Microsoft.Playwright.Assertions;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Tests E2E de la page LoginWith2fa : le compte est protégé par MFA via l'IHM, puis une session vierge se connecte.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class LoginWith2faPageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : AuthenticatedE2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task LoginWith2faPage_WhenPasswordIsValidOnMfaAccount_DisplaysCodePrompt()
    {
        // Arrange
        var email = await RegisterAndSignInAsync();
        await EnableMfaAsync();
        var freshPage = await PlaywrightFixture.CreatePageAsync();
        try
        {
            var loginPage = new LoginPage(freshPage);
            var twoFactorPage = new LoginWith2faPage(freshPage);
            await loginPage.NavigateAsync(BaseUrl);

            // Act
            await loginPage.LoginAsync(email, "Password123");
            await freshPage.WaitForURLAsync("**/Security/LoginWith2fa*");

            // Assert
            await Expect(twoFactorPage.MainHeading).ToBeVisibleAsync();
            await Expect(twoFactorPage.CodeInput).ToBeVisibleAsync();
        }
        finally
        {
            var context = freshPage.Context;
            await freshPage.CloseAsync();
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task LoginWith2faPage_WhenValidTotpCodeIsSubmitted_RedirectsToAccounts()
    {
        // Arrange
        var email = await RegisterAndSignInAsync();
        var sharedKey = await EnableMfaAsync();
        var freshPage = await PlaywrightFixture.CreatePageAsync();
        try
        {
            var loginPage = new LoginPage(freshPage);
            var twoFactorPage = new LoginWith2faPage(freshPage);
            await loginPage.NavigateAsync(BaseUrl);
            await loginPage.LoginAsync(email, "Password123");
            await freshPage.WaitForURLAsync("**/Security/LoginWith2fa*");

            // Act
            await twoFactorPage.SubmitCodeAsync(TotpCodeGenerator.Generate(sharedKey, DateTimeOffset.UtcNow));

            // Assert
            await freshPage.WaitForURLAsync("**/Accounts*");
            freshPage.Url.Should().Contain("/Accounts");
        }
        finally
        {
            var context = freshPage.Context;
            await freshPage.CloseAsync();
            await context.CloseAsync();
        }
    }

    private async Task<string> EnableMfaAsync()
    {
        var mfaPage = new EnableMfaPage(Page);
        await mfaPage.NavigateAsync(BaseUrl);
        var sharedKey = await mfaPage.ReadSharedKeyAsync();
        await mfaPage.ConfirmWithCodeAsync(TotpCodeGenerator.Generate(sharedKey, DateTimeOffset.UtcNow));
        await Expect(mfaPage.ActivationConfirmation.First).ToBeVisibleAsync();
        return sharedKey;
    }
}
