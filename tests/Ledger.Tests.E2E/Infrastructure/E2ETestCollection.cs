namespace Ledger.Tests.E2E.Infrastructure;

/// <summary>
/// Définition de collection xUnit regroupant les fixtures serveur Web et navigateur Playwright.
/// </summary>
[CollectionDefinition(Name)]
public sealed class E2ETestCollection : ICollectionFixture<AppWebTestFixture>, ICollectionFixture<PlaywrightFixture>
{
    /// <summary>
    /// Nom de la collection de tests E2E Playwright.
    /// </summary>
    public const string Name = "E2E";
}
