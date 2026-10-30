using System.ComponentModel.DataAnnotations;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Accounts.Pages;

[Authorize]
public class CreateAccountModel(
    AppDbContext dbContext,
    IAccountTypeService accountTypeService,
    ICurrencyService currencyService,
    ILocalizedEntityService localizedEntityService) : PageModel
{
    private readonly AccountFormOptionsLoader _formOptions = new(dbContext, accountTypeService, currencyService, localizedEntityService);

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<Bank> AvailableBanks { get; set; } = new();
    public List<CustomAccountType> AvailableAccountTypes { get; set; } = new();
    public Dictionary<Guid, string> LocalizedTypeNames { get; set; } = new();
    public IReadOnlyList<CurrencyInfo> AvailableCurrencies { get; set; } = new List<CurrencyInfo>();

    public class InputModel
    {
        [Display(Name = "Banque existante")]
        public Guid? ExistingBankId { get; set; }

        [Display(Name = "Ou créer une nouvelle banque")]
        public string? NewBankName { get; set; }

        [Required(ErrorMessage = "Le nom du compte est requis.")]
        [Display(Name = "Nom du compte")]
        public string AccountName { get; set; } = string.Empty;

        [Display(Name = "Type de compte")]
        public Guid? CustomAccountTypeId { get; set; }

        [Display(Name = "Type de compte (standard)")]
        public AccountType Type { get; set; } = AccountType.Checking;

        [Display(Name = "IBAN")]
        public string? Iban { get; set; }

        [Display(Name = "Identifiant bancaire OFX (<ACCTID>)")]
        public string? OfxAccountId { get; set; }

        [Display(Name = "Solde initial")]
        public decimal InitialBalance { get; set; } = 0.00m;

        [Display(Name = "Devise")]
        public string Currency { get; set; } = "EUR";
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        await LoadFormOptionsAsync(cancellationToken);

        // Sélectionner par défaut le type Compte de Dépôt s'il existe
        var defaultType = AvailableAccountTypes.FirstOrDefault(t => string.Equals(t.Code, "Checking", StringComparison.Ordinal)) ?? AvailableAccountTypes.FirstOrDefault();
        if (defaultType != null)
        {
            Input.CustomAccountTypeId = defaultType.Id;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await LoadFormOptionsAsync(cancellationToken);
            return Page();
        }

        Guid bankId;
        if (Input.ExistingBankId.HasValue && Input.ExistingBankId != Guid.Empty)
        {
            bankId = Input.ExistingBankId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(Input.NewBankName))
        {
            var newBank = new Bank
            {
                UserId = string.Empty, // Injecté automatiquement par SaveChangesAsync
                Name = Input.NewBankName.Trim()
            };
            dbContext.Banks.Add(newBank);
            await dbContext.SaveChangesAsync(cancellationToken);
            bankId = newBank.Id;
        }
        else
        {
            ModelState.AddModelError("Input.ExistingBankId", "Veuillez sélectionner une banque existante ou saisir le nom d'une nouvelle.");
            await LoadFormOptionsAsync(cancellationToken, includeTypeNames: false);
            return Page();
        }

        // Résoudre le fallback AccountType enum à partir du type personnalisé choisi
        var legacyType = await LegacyAccountTypeResolver.ResolveAsync(accountTypeService, Input.CustomAccountTypeId, Input.Type, cancellationToken);

        var account = new BankAccount
        {
            UserId = string.Empty, // Injecté automatiquement par SaveChangesAsync
            BankId = bankId,
            Name = Input.AccountName.Trim(),
            CustomAccountTypeId = Input.CustomAccountTypeId,
            Type = legacyType,
            Iban = Input.Iban?.Trim(),
            OfxAccountId = Input.OfxAccountId?.Trim(),
            InitialBalance = Input.InitialBalance,
            Currency = string.IsNullOrWhiteSpace(Input.Currency) ? "EUR" : Input.Currency.ToUpperInvariant()
        };

        dbContext.BankAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Redirect("/Accounts");
    }

    private async Task LoadFormOptionsAsync(CancellationToken cancellationToken, bool includeTypeNames = true)
    {
        var options = await _formOptions.LoadAsync(cancellationToken);
        AvailableBanks = options.Banks;
        AvailableAccountTypes = options.AccountTypes;
        AvailableCurrencies = options.Currencies;
        if (includeTypeNames)
        {
            LocalizedTypeNames = await _formOptions.LoadTypeNamesAsync(options.AccountTypes, cancellationToken);
        }
    }
}
