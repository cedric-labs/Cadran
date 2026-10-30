using AwesomeAssertions;
using Ledger.Features.Security.Context;
using Ledger.Tests.E2E.Infrastructure;
using Ledger.Tests.E2E.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using static Microsoft.Playwright.Assertions;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Test E2E de la page UnlockDatabase, atteinte en forçant l'état verrouillé du porteur de clé du serveur de test.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class UnlockDatabasePageTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    [Fact]
    public async Task UnlockDatabasePage_WhenDatabaseIsLocked_DisplaysPassphraseForm()
    {
        // Arrange
        // KestrelServices ne désigne l'hôte réel qu'après son démarrage ; avant, il renvoie l'hôte de test, dont le porteur de clé n'est pas celui qui sert les requêtes.
        WebFixture.EnsureServerStarted();
        var keyHolder = WebFixture.KestrelServices.GetRequiredService<IDatabaseKeyHolder>();
        var page = new UnlockDatabasePage(Page);
        try
        {
            keyHolder.SetKey(null, isEncrypted: true);

            // Act
            var response = await page.NavigateAsync(BaseUrl);

            // Assert
            response.Should().NotBeNull();
            response.Status.Should().Be(StatusCodes.Status200OK);
            await Expect(page.MainHeading).ToBeVisibleAsync();
            await Expect(page.PassphraseInput).ToBeVisibleAsync();
        }
        finally
        {
            // Le serveur est partagé par toute la collection : la base doit redevenir accessible pour les tests suivants.
            keyHolder.SetKey(null, isEncrypted: false);
        }
    }
}
