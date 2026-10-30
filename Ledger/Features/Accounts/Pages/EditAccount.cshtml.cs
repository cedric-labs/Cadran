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
    private readonly AccountFormOptionsLoader _formOptions = new(dbContext, accountTypeService, currencyService, localizedEntityService);

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

        ReturnUrl = string.IsNullOrWhiteSpace(ReturnUrl) ? ResolveLocalReturnUrlFromReferer() : ReturnUrl;

        var account = await dbContext.BankAccounts
            .Include(a => a.Bank)
            .Include(a => a.CustomAccountType)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (account == null)
        {
            return NotFound();
        }

        await LoadFormOptionsAsync(cancellationToken);

        // Si le compte n'a pas encore de CustomAccountTypeId, le relier au type par défaut correspondant
        var customTypeId = account.CustomAccountTypeId;
        if (!customTypeId.HasValue)
        {
            var match = AvailableAccountTypes.FirstOrDefault(t => string.Equals(t.Code, account.Type.ToString(), StringComparison.Ordinal))
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
            await LoadFormOptionsAsync(cancellationToken);
            return Page();
        }

        var bankId = await ResolveBankIdAsync(account, cancellationToken);

        // Mettre à jour l'enum fallback
        var legacyType = await LegacyAccountTypeResolver.ResolveAsync(accountTypeService, Input.CustomAccountTypeId, Input.Type, cancellationToken);

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

    // Le référent n'est repris que s'il désigne une adresse locale, pour écarter toute redirection ouverte.
    private string? ResolveLocalReturnUrlFromReferer()
    {
        var referer = Request.Headers.Referer.ToString();
        if (string.IsNullOrEmpty(referer) || !Uri.TryCreate(referer, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return Url.IsLocalUrl(uri.PathAndQuery) ? uri.PathAndQuery : null;
    }

    private async Task<Guid> ResolveBankIdAsync(BankAccount account, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Input.NewBankName))
        {
            var newBank = new Bank
            {
                UserId = string.Empty,
                Name = Input.NewBankName.Trim()
            };
            dbContext.Banks.Add(newBank);
            await dbContext.SaveChangesAsync(cancellationToken);
            return newBank.Id;
        }

        return Input.ExistingBankId.HasValue && Input.ExistingBankId != Guid.Empty ? Input.ExistingBankId.Value : account.BankId;
    }
}
