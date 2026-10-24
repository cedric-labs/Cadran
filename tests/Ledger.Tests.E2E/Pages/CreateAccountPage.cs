using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Pages;

/// <summary>
/// Page Object représentant le formulaire d'ajout d'un compte bancaire /Accounts/CreateAccount.
/// </summary>
public sealed class CreateAccountPage(IPage page) : BasePage(page)
{
    private ILocator Heading => Page.Locator("h1.h4");
    private ILocator AccountNameInput => Page.Locator("input[name='Input.AccountName']");
    private ILocator NewBankNameInput => Page.Locator("input[name='Input.NewBankName']");
    private ILocator CurrencySelect => Page.Locator("select[name='Input.Currency']");
    private ILocator SubmitButton => Page.Locator(".card form button[type='submit']");
    private ILocator AccountNameError => Page.Locator("span[data-valmsg-for='Input.AccountName']");

    /// <summary>
    /// Navigue directement vers la page de création d'un compte.
    /// </summary>
    public Task NavigateAsync(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        var url = new Uri(new Uri(baseUrl), "/Accounts/CreateAccount").ToString();
        return Page.GotoAsync(url);
    }

    /// <summary>
    /// Vérifie si les champs du formulaire sont visibles.
    /// </summary>
    public async Task<bool> IsFormVisibleAsync()
    {
        return await Heading.IsVisibleAsync() &&
               await AccountNameInput.IsVisibleAsync() &&
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
    /// Saisit le libellé du nouveau compte.
    /// </summary>
    public Task FillAccountNameAsync(string name) => AccountNameInput.FillAsync(name);

    /// <summary>
    /// Saisit le nom de la nouvelle banque.
    /// </summary>
    public Task FillNewBankNameAsync(string bankName) => NewBankNameInput.FillAsync(bankName);

    /// <summary>
    /// Sélectionne la devise du compte.
    /// </summary>
    public Task SelectCurrencyAsync(string currencyCode) => CurrencySelect.SelectOptionAsync(currencyCode);

    /// <summary>
    /// Soumet le formulaire.
    /// </summary>
    public Task SubmitAsync() => SubmitButton.ClickAsync();

    /// <summary>
    /// Récupère le message d'erreur de validation pour le nom du compte.
    /// </summary>
    public async Task<string> GetAccountNameErrorAsync()
    {
        return (await AccountNameError.InnerTextAsync()).Trim();
    }
}
