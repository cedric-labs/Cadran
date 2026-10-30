using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object de la page /Security/EnableMfa.
/// </summary>
public sealed class EnableMfaPage(IPage page) : BasePage(page)
{
    /// <summary>
    /// Titre principal de la page.
    /// </summary>
    public ILocator MainHeading => Page.GetByRole(AriaRole.Heading, new() { Level = 1 });

    private ILocator SharedKeyText => Page.GetByTestId("mfa-shared-key");

    private ILocator CodeInput => Page.Locator("input[name='Input.Code']");

    private ILocator SuccessAlert => Page.GetByRole(AriaRole.Alert);

    /// <summary>
    /// Confirmation affichée une fois l'authentification à deux facteurs activée.
    /// </summary>
    public ILocator ActivationConfirmation => SuccessAlert;

    /// <summary>
    /// Lit la clé partagée affichée pour l'enregistrement dans l'application d'authentification.
    /// </summary>
    public async Task<string> ReadSharedKeyAsync() => (await SharedKeyText.InnerTextAsync()).Trim();

    /// <summary>
    /// Saisit le code de confirmation et soumet le formulaire d'activation.
    /// </summary>
    public async Task ConfirmWithCodeAsync(string code)
    {
        await CodeInput.FillAsync(code);
        await Page.Locator("form:has(input[name='Input.Code']) button[type='submit']").ClickAsync();
    }

    /// <summary>
    /// Navigue vers la page et renvoie la réponse HTTP du serveur.
    /// </summary>
    public Task<IResponse?> NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return Page.GotoAsync(new Uri(new Uri(baseUrl), "/Security/EnableMfa").ToString());
    }
}
