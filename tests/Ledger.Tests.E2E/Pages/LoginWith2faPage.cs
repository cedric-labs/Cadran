using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object de la page /Security/LoginWith2fa, atteinte après un mot de passe valide sur un compte protégé par MFA.
/// </summary>
public sealed class LoginWith2faPage(IPage page) : BasePage(page)
{
    /// <summary>
    /// Titre principal de la page.
    /// </summary>
    public ILocator MainHeading => Page.GetByRole(AriaRole.Heading, new() { Level = 2 });

    /// <summary>
    /// Champ de saisie du code TOTP.
    /// </summary>
    public ILocator CodeInput => Page.Locator("#inputTwoFactorCode");

    /// <summary>
    /// Saisit le code TOTP et valide le formulaire.
    /// </summary>
    public async Task SubmitCodeAsync(string code)
    {
        await CodeInput.FillAsync(code);
        await Page.Locator("form:has(#inputTwoFactorCode) button[type='submit']").ClickAsync();
    }
}
