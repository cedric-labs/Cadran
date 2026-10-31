using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object de la page /Transactions.
/// </summary>
public sealed class TransactionsPage(IPage page) : BasePage(page)
{
    /// <summary>
    /// Titre principal de la page.
    /// </summary>
    public ILocator MainHeading => Page.GetByRole(AriaRole.Heading, new() { Level = 1 });

    /// <summary>Bouton d'ouverture de la modale de saisie manuelle.</summary>
    public ILocator ManualEntryOpenButton => Page.GetByTestId("manual-entry-open");

    /// <summary>Modale de saisie manuelle.</summary>
    public ILocator ManualEntryModal => Page.GetByTestId("manual-entry-modal");

    /// <summary>Message de résultat affiché après la soumission de la saisie manuelle.</summary>
    public ILocator ManualEntryResult => Page.GetByTestId("manual-entry-result");

    /// <summary>Badges d'origine « saisie manuelle » présents dans le registre.</summary>
    public ILocator ManualOriginBadges => Page.Locator("[data-testid^='tx-origin-manual-']");

    /// <summary>
    /// Ouvre la modale, saisit un débit et le soumet.
    /// </summary>
    public async Task CreateManualDebitAsync(string label, string amount)
    {
        await ManualEntryOpenButton.ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(ManualEntryModal).ToBeVisibleAsync();
        await ManualEntryModal.GetByLabel("Libellé").FillAsync(label);
        await ManualEntryModal.GetByLabel("Montant").FillAsync(amount);
        await ManualEntryModal.GetByTestId("manual-entry-submit").ClickAsync();
    }

    /// <summary>
    /// Navigue vers la page et renvoie la réponse HTTP du serveur.
    /// </summary>
    public Task<IResponse?> NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return Page.GotoAsync(new Uri(new Uri(baseUrl), "/Transactions").ToString());
    }
}
