using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object représentant la page de création de compte /Security/Register.
/// </summary>
public sealed class RegisterPage(IPage page) : BasePage(page)
{
    private ILocator Heading => Page.Locator("h2.h4");
    private ILocator FullNameInput => Page.Locator("input[name='Input.FullName']");
    private ILocator EmailInput => Page.Locator("input[name='Input.Email']");
    private ILocator PasswordInput => Page.Locator("input[name='Input.Password']");
    private ILocator ConfirmPasswordInput => Page.Locator("input[name='Input.ConfirmPassword']");
    private ILocator SubmitButton => Page.Locator("button[type='submit']");
    private ILocator ValidationSummary => Page.Locator("div[asp-validation-summary], div.text-danger.small");
    private ILocator FullNameValidationError => Page.Locator("span[data-valmsg-for='Input.FullName']");
    private ILocator EmailValidationError => Page.Locator("span[data-valmsg-for='Input.Email']");
    private ILocator PasswordValidationError => Page.Locator("span[data-valmsg-for='Input.Password']");
    private ILocator ConfirmPasswordValidationError => Page.Locator("span[data-valmsg-for='Input.ConfirmPassword']");
    private ILocator LoginLink => Page.Locator(".card-body a[href='/Security/Login']");

    /// <summary>
    /// Navigue directement vers la page d'inscription.
    /// </summary>
    public Task NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        var url = new Uri(new Uri(baseUrl), "/Security/Register").ToString();
        return Page.GotoAsync(url);
    }

    /// <summary>
    /// Vérifie si l'ensemble des champs du formulaire d'inscription sont visibles.
    /// </summary>
    public async Task<bool> IsFormVisibleAsync()
    {
        return await FullNameInput.IsVisibleAsync() &&
               await EmailInput.IsVisibleAsync() &&
               await PasswordInput.IsVisibleAsync() &&
               await ConfirmPasswordInput.IsVisibleAsync() &&
               await SubmitButton.IsVisibleAsync();
    }

    /// <summary>
    /// Récupère le titre de la page d'inscription.
    /// </summary>
    public async Task<string> GetHeadingTextAsync()
    {
        return (await Heading.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Saisit le nom complet.
    /// </summary>
    public Task FillFullNameAsync(string fullName) => FullNameInput.FillAsync(fullName);

    /// <summary>
    /// Saisit l'adresse email.
    /// </summary>
    public Task FillEmailAsync(string email) => EmailInput.FillAsync(email);

    /// <summary>
    /// Saisit le mot de passe.
    /// </summary>
    public Task FillPasswordAsync(string password) => PasswordInput.FillAsync(password);

    /// <summary>
    /// Saisit la confirmation du mot de passe.
    /// </summary>
    public Task FillConfirmPasswordAsync(string confirmPassword) => ConfirmPasswordInput.FillAsync(confirmPassword);

    /// <summary>
    /// Soumet le formulaire d'inscription.
    /// </summary>
    public Task SubmitAsync() => SubmitButton.ClickAsync();

    /// <summary>
    /// Remplit l'intégralité du formulaire et soumet la création de compte.
    /// </summary>
    public async Task RegisterAsync(string fullName, string email, string password, string confirmPassword)
    {
        await FillFullNameAsync(fullName);
        await FillEmailAsync(email);
        await FillPasswordAsync(password);
        await FillConfirmPasswordAsync(confirmPassword);
        await SubmitAsync();
    }

    /// <summary>
    /// Récupère l'erreur de validation du nom complet.
    /// </summary>
    public async Task<string> GetFullNameValidationErrorAsync()
    {
        return (await FullNameValidationError.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère l'erreur de validation de l'email.
    /// </summary>
    public async Task<string> GetEmailValidationErrorAsync()
    {
        return (await EmailValidationError.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère l'erreur de validation du mot de passe.
    /// </summary>
    public async Task<string> GetPasswordValidationErrorAsync()
    {
        return (await PasswordValidationError.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère l'erreur de validation de la confirmation du mot de passe.
    /// </summary>
    public async Task<string> GetConfirmPasswordValidationErrorAsync()
    {
        return (await ConfirmPasswordValidationError.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Récupère les erreurs du résumé de validation.
    /// </summary>
    public async Task<string> GetValidationSummaryTextAsync()
    {
        return (await ValidationSummary.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Clique sur le lien "Déjà un compte ? Se connecter".
    /// </summary>
    public Task ClickLoginLinkAsync() => LoginLink.ClickAsync();
}
