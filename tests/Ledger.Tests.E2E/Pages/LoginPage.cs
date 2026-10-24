using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object représentant la page d'authentification /Security/Login.
/// </summary>
public sealed class LoginPage(IPage page) : BasePage(page)
{
    private ILocator Heading => Page.Locator("h2.h4");
    private ILocator EmailInput => Page.Locator("input[name='Input.Email']");
    private ILocator PasswordInput => Page.Locator("input[name='Input.Password']");
    private ILocator RememberMeCheckbox => Page.Locator("input[name='Input.RememberMe']");
    private ILocator SubmitButton => Page.Locator("button[type='submit']");
    private ILocator ValidationSummary => Page.Locator("div[asp-validation-summary], div.text-danger.small");
    private ILocator EmailValidationError => Page.Locator("span[data-valmsg-for='Input.Email']");
    private ILocator PasswordValidationError => Page.Locator("span[data-valmsg-for='Input.Password']");
    private ILocator RegisterLink => Page.Locator(".card-body a[href='/Security/Register']");

    /// <summary>
    /// Navigue directement vers la page de connexion.
    /// </summary>
    public Task NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        var url = new Uri(new Uri(baseUrl), "/Security/Login").ToString();
        return Page.GotoAsync(url);
    }

    /// <summary>
    /// Vérifie si le formulaire de connexion est affiché.
    /// </summary>
    public async Task<bool> IsFormVisibleAsync()
    {
        return await EmailInput.IsVisibleAsync() &&
               await PasswordInput.IsVisibleAsync() &&
               await SubmitButton.IsVisibleAsync();
    }

    /// <summary>
    /// Récupère le titre du formulaire.
    /// </summary>
    public async Task<string> GetHeadingTextAsync()
    {
        return (await Heading.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Saisit l'adresse email de connexion.
    /// </summary>
    public Task FillEmailAsync(string email) => EmailInput.FillAsync(email);

    /// <summary>
    /// Saisit le mot de passe.
    /// </summary>
    public Task FillPasswordAsync(string password) => PasswordInput.FillAsync(password);

    /// <summary>
    /// Coche ou décoche l'option Se souvenir de moi.
    /// </summary>
    public Task SetRememberMeAsync(bool check) => RememberMeCheckbox.SetCheckedAsync(check);

    /// <summary>
    /// Soumet le formulaire de connexion.
    /// </summary>
    public Task SubmitAsync() => SubmitButton.ClickAsync();

    /// <summary>
    /// Exécute la saisie des identifiants et la soumission du formulaire.
    /// </summary>
    public async Task LoginAsync(string email, string password)
    {
        await FillEmailAsync(email);
        await FillPasswordAsync(password);
        await SubmitAsync();
    }

    /// <summary>
    /// Récupère le message d'erreur de validation pour le champ email.
    /// </summary>
    public async Task<string> GetEmailValidationErrorAsync()
    {
        return (await EmailValidationError.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère le message d'erreur de validation pour le champ mot de passe.
    /// </summary>
    public async Task<string> GetPasswordValidationErrorAsync()
    {
        return (await PasswordValidationError.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère le texte du résumé de validation s'il est affiché.
    /// </summary>
    public async Task<string> GetValidationSummaryTextAsync()
    {
        return (await ValidationSummary.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Clique sur le lien menant vers la création de compte.
    /// </summary>
    public Task ClickRegisterLinkAsync() => RegisterLink.ClickAsync();
}
