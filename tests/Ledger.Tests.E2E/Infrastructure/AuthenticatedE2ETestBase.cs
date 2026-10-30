using Ledger.Tests.E2E.Pages;

namespace Ledger.Tests.E2E.Infrastructure;

/// <summary>
/// Base des tests E2E dont la page ciblée exige une session authentifiée.
/// </summary>
public abstract class AuthenticatedE2ETestBase(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    /// <summary>
    /// Crée un utilisateur unique par l'IHM d'inscription, attend l'arrivée sur la liste des comptes et renvoie son adresse email.
    /// </summary>
    protected async Task<string> RegisterAndSignInAsync()
    {
        var registerPage = new RegisterPage(Page);
        await registerPage.NavigateAsync(BaseUrl);
        var uniqueEmail = $"e2e_{Guid.NewGuid():N}@Ledger.local";
        await registerPage.RegisterAsync("Utilisateur E2E", uniqueEmail, "Password123", "Password123");
        await Page.WaitForURLAsync("**/Accounts*");
        return uniqueEmail;
    }

    /// <summary>
    /// Crée un compte bancaire par l'IHM et renvoie son identifiant lu dans le lien de détail de la liste.
    /// </summary>
    protected async Task<Guid> CreateAccountAndGetIdAsync()
    {
        var createAccountPage = new CreateAccountPage(Page);
        await createAccountPage.NavigateAsync(BaseUrl);
        await createAccountPage.FillAccountNameAsync("Compte courant E2E");
        await createAccountPage.FillNewBankNameAsync("Banque E2E");
        await createAccountPage.SubmitAsync();
        await Page.WaitForURLAsync(url => !url.Contains("/CreateAccount", StringComparison.Ordinal));

        var accountsListPage = new AccountsListPage(Page);
        await accountsListPage.NavigateAsync(BaseUrl);
        return await accountsListPage.GetFirstAccountIdAsync();
    }
}
