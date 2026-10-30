using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Accounts.Pages;

/// <summary>
/// Charge les listes proposées par les formulaires de création et de modification de compte : banques, types de comptes et devises.
/// </summary>
internal sealed class AccountFormOptionsLoader(
    AppDbContext dbContext,
    IAccountTypeService accountTypeService,
    ICurrencyService currencyService,
    ILocalizedEntityService localizedEntityService)
{
    /// <summary>
    /// Banques triées par nom, types de comptes et devises prises en charge.
    /// </summary>
    public async Task<AccountFormOptions> LoadAsync(CancellationToken cancellationToken)
    {
        var banks = await dbContext.Banks.OrderBy(b => b.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
        var accountTypes = await accountTypeService.GetAllAccountTypesAsync(cancellationToken).ConfigureAwait(false);
        return new AccountFormOptions(banks, accountTypes, currencyService.GetSupportedCurrencies());
    }

    /// <summary>
    /// Noms des types de comptes traduits dans la langue de l'interface.
    /// </summary>
    public Task<Dictionary<Guid, string>> LoadTypeNamesAsync(IEnumerable<CustomAccountType> accountTypes, CancellationToken cancellationToken)
    {
        return localizedEntityService.GetTranslationsAsync(
            "CustomAccountType",
            accountTypes.Select(t => t.Id),
            "Name",
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            cancellationToken);
    }
}

/// <summary>Listes de choix d'un formulaire de compte.</summary>
internal sealed record AccountFormOptions(List<Bank> Banks, List<CustomAccountType> AccountTypes, IReadOnlyList<CurrencyInfo> Currencies);
