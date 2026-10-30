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
public class EditAccountModel(
    AppDbContext dbContext,
    IAccountTypeService accountTypeService,
    ICurrencyService currencyService,
    Ledger.Infrastructure.Configuration.ILocalizedEntityService localizedEntityService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public List<Bank> AvailableBanks { get; set; } = new();
    public List<CustomAccountType> AvailableAccountTypes { get; set; } = new();
    public Dictionary<Guid, string> LocalizedTypeNames { get; set; } = new();
    public IReadOnlyList<CurrencyInfo> AvailableCurrencies { get; set; } = new List<CurrencyInfo>();

    public Guid AccountId { get; set; }

    public class InputModel
    {
        [Display(Name = "Banque existante")]
        public Guid? ExistingBankId { get; set; }

        [Display(Name = "Ou nouvelle banque")]
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

        [Display(Name = "Solde initial (au début de l'historique)")]
        public decimal InitialBalance { get; set; } = 0.00m;

        [Display(Name = "Devise")]
        public string Currency { get; set; } = "EUR";

        [Display(Name = "Compte archivé")]
        public bool IsArchived { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        AccountId = id;
        ReturnUrl = returnUrl;

        if (string.IsNullOrWhiteSpace(ReturnUrl))
        {
            var referer = Request.Headers.Referer.ToString();
            if (!string.IsNullOrEmpty(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            {
                var path = uri.PathAndQuery;
                if (Url.IsLocalUrl(path))
                {
                    ReturnUrl = path;
                }
            }
        }

        var account = await dbContext.BankAccounts
            .Include(a => a.Bank)
            .Include(a => a.CustomAccountType)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (account == null)
        {
            return NotFound();
        }

        AvailableBanks = await dbContext.Banks.OrderBy(b => b.Name).ToListAsync(cancellationToken);
        AvailableAccountTypes = await accountTypeService.GetAllAccountTypesAsync(cancellationToken);
        AvailableCurrencies = currencyService.GetSupportedCurrencies();

        LocalizedTypeNames = await localizedEntityService.GetTranslationsAsync(
            "CustomAccountType",
            AvailableAccountTypes.Select(t => t.Id),
            "Name",
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            cancellationToken);

        // Si le compte n'a pas encore de CustomAccountTypeId, le relier au type par défaut correspondant
        var customTypeId = account.CustomAccountTypeId;
        if (!customTypeId.HasValue)
        {
            var match = AvailableAccountTypes.FirstOrDefault(t => t.Code == account.Type.ToString())
                     ?? AvailableAccountTypes.FirstOrDefault();
            customTypeId = match?.Id;
        }

        Input = new InputModel
        {
            ExistingBankId = account.BankId,
            AccountName = account.Name,
            CustomAccountTypeId = customTypeId,
            Type = account.Type,
            Iban = account.Iban,
            OfxAccountId = account.OfxAccountId,
            InitialBalance = account.InitialBalance,
            Currency = account.Currency,
            IsArchived = account.IsArchived
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AccountId = id;

        var account = await dbContext.BankAccounts
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (account == null)
        {
            return NotFound();
        }

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
        if (!string.IsNullOrWhiteSpace(Input.NewBankName))
        {
            var newBank = new Bank
            {
                UserId = string.Empty,
                Name = Input.NewBankName.Trim()
            };
            dbContext.Banks.Add(newBank);
            await dbContext.SaveChangesAsync(cancellationToken);
            bankId = newBank.Id;
        }
        else if (Input.ExistingBankId.HasValue && Input.ExistingBankId != Guid.Empty)
        {
            bankId = Input.ExistingBankId.Value;
        }
        else
        {
            bankId = account.BankId;
        }

        // Mettre à jour l'enum fallback
        var legacyType = Input.Type;
        if (Input.CustomAccountTypeId.HasValue)
        {
            var chosenType = await accountTypeService.GetByIdAsync(Input.CustomAccountTypeId.Value, cancellationToken);
            if (chosenType != null && !string.IsNullOrEmpty(chosenType.Code) && Enum.TryParse<AccountType>(chosenType.Code, out var parsedType))
            {
                legacyType = parsedType;
            }
        }

        account.BankId = bankId;
        account.Name = Input.AccountName.Trim();
        account.CustomAccountTypeId = Input.CustomAccountTypeId;
        account.Type = legacyType;
        account.Iban = Input.Iban?.Trim();
        account.OfxAccountId = Input.OfxAccountId?.Trim();
        account.InitialBalance = Input.InitialBalance;
        account.Currency = string.IsNullOrWhiteSpace(Input.Currency) ? "EUR" : Input.Currency.ToUpperInvariant();
        account.IsArchived = Input.IsArchived;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return Redirect($"/Accounts/Details/{id}");
    }
}
