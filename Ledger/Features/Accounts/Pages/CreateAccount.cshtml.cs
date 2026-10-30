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
        AvailableBanks = await dbContext.Banks.OrderBy(b => b.Name).ToListAsync(cancellationToken);
        AvailableAccountTypes = await accountTypeService.GetAllAccountTypesAsync(cancellationToken);
        AvailableCurrencies = currencyService.GetSupportedCurrencies();

        LocalizedTypeNames = await localizedEntityService.GetTranslationsAsync(
            "CustomAccountType",
            AvailableAccountTypes.Select(t => t.Id),
            "Name",
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            cancellationToken);

        // Sélectionner par défaut le type Compte de Dépôt s'il existe
        var defaultType = AvailableAccountTypes.FirstOrDefault(t => t.Code == "Checking") ?? AvailableAccountTypes.FirstOrDefault();
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
            AvailableBanks = await dbContext.Banks.OrderBy(b => b.Name).ToListAsync(cancellationToken);
            AvailableAccountTypes = await accountTypeService.GetAllAccountTypesAsync(cancellationToken);
            AvailableCurrencies = currencyService.GetSupportedCurrencies();
            LocalizedTypeNames = await localizedEntityService.GetTranslationsAsync(
                "CustomAccountType",
                AvailableAccountTypes.Select(t => t.Id),
                "Name",
                System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                cancellationToken);
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
            AvailableBanks = await dbContext.Banks.OrderBy(b => b.Name).ToListAsync(cancellationToken);
            AvailableAccountTypes = await accountTypeService.GetAllAccountTypesAsync(cancellationToken);
            AvailableCurrencies = currencyService.GetSupportedCurrencies();
            return Page();
        }

        // Résoudre le fallback AccountType enum à partir du type personnalisé choisi
        var legacyType = Input.Type;
        if (Input.CustomAccountTypeId.HasValue)
        {
            var chosenType = await accountTypeService.GetByIdAsync(Input.CustomAccountTypeId.Value, cancellationToken);
            if (chosenType != null && !string.IsNullOrEmpty(chosenType.Code) && Enum.TryParse<AccountType>(chosenType.Code, out var parsedType))
            {
                legacyType = parsedType;
            }
        }

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
}
