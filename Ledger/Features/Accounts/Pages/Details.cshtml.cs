using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Merchants.Services;
using Ledger.Features.RulesEngine.Services;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Accounts.Pages;

[Authorize]
public class DetailsModel(
    AppDbContext dbContext,
    IAccountBalanceService balanceService,
    IRulesEngineService rulesEngineService,
    ITransactionStatusService statusService,
    IUiSettingsService uiSettingsService,
    Ledger.Features.Recurring.Services.IRecurringExpenseService recurringExpenseService,
    Ledger.Infrastructure.Configuration.ILocalizedEntityService localizedEntityService,
    IMerchantService merchantService,
    TimeProvider timeProvider)
    : TransactionOperationsPageModelBase(
        dbContext,
        statusService,
        uiSettingsService,
        rulesEngineService,
        merchantService,
        recurringExpenseService,
        localizedEntityService,
        timeProvider)
{
    public override string DefaultTableKey => "AccountDetails";

    protected override string GetDefaultRedirectUrl(Transaction? transaction = null)
    {
        return transaction != null ? $"/Accounts/Details/{transaction.BankAccountId}" : "/Accounts";
    }

    public BankAccount? Account { get; set; }
    public AccountBalanceSummaryDto? BalanceSummary { get; set; }
    public string? LocalizedAccountTypeName { get; set; }

    public decimal TotalFilteredDebits { get; set; }
    public decimal TotalFilteredCredits { get; set; }
    public decimal TotalFilteredSalaryCredits { get; set; }
    public decimal TotalFilteredOtherCredits { get; set; }
    public decimal FilteredNetTotal => TotalFilteredCredits - TotalFilteredDebits;
    public int TotalFilteredCount { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalFilteredCount / (EffectivePageSize > 0 ? EffectivePageSize : 50));
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int StartItemIndex => TotalFilteredCount == 0 ? 0 : (PageNumber - 1) * EffectivePageSize + 1;
    public int EndItemIndex => Math.Min(PageNumber * EffectivePageSize, TotalFilteredCount);

    public bool HasActiveFilters => HasBaseActiveFilters;
    public int ActiveFilterCriteriaCount => BaseFilterCriteriaCount;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var defaultPeriod = await LoadListingSettingsAsync(cancellationToken);

        var resolvedPeriod = BudgetPeriodCalculator.ResolvePeriod(Period, StartDate, EndDate, defaultPeriod);
        StartDate = resolvedPeriod.StartDate;
        EndDate = resolvedPeriod.EndDate;
        Period = resolvedPeriod.Period;

        Account = await DbContext.BankAccounts
            .Include(a => a.Bank)
            .Include(a => a.CustomAccountType)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (Account == null)
        {
            return NotFound();
        }

        if (Account.CustomAccountType != null)
        {
            LocalizedAccountTypeName = await LocalizedEntityService.GetTranslationAsync(
                "CustomAccountType",
                Account.CustomAccountType.Id,
                "Name",
                Account.CustomAccountType.Name,
                cancellationToken: cancellationToken);
        }

        BalanceSummary = await balanceService.GetAccountBalanceAsync(id, cancellationToken);

        await LoadReferenceSelectorsAsync(DefaultTableKey, id, cancellationToken);

        AvailableAccounts = await DbContext.BankAccounts
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        var query = DbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.CustomStatus)
            .Include(t => t.Merchant)
            .Include(t => t.Splits)
                .ThenInclude(s => s.Category)
            .Include(t => t.Splits)
                .ThenInclude(s => s.Merchant)
            .Where(t => t.BankAccountId == id)
            .AsQueryable();

        query = await ApplyCommonTransactionFiltersAsync(query, cancellationToken);

        var allFiltered = await query.ToListAsync(cancellationToken);
        allFiltered = await ApplyPostEvaluationFiltersAsync(allFiltered, cancellationToken);

        var totals = TransactionTotals.Of(allFiltered);
        TotalFilteredCount = totals.Count;
        TotalFilteredDebits = totals.Debits;
        TotalFilteredCredits = totals.Credits;
        TotalFilteredSalaryCredits = totals.SalaryCredits;
        TotalFilteredOtherCredits = totals.OtherCredits;

        await PaginateAndLoadMatchesAsync(allFiltered, cancellationToken);

        return Page();
    }

    public string GetPageUrl(int page)
    {
        var qs = BuildPageQueryString(page);
        return $"/Accounts/Details/{Account?.Id}?{qs}";
    }

    public async Task<IActionResult> OnPostApplyRulesAsync(Guid id, bool overwriteExisting = false, CancellationToken cancellationToken = default)
    {
        var count = await RulesEngineService.ApplyRulesToDatabaseAsync(
            overwriteExisting: overwriteExisting,
            bankAccountId: id,
            cancellationToken: cancellationToken);

        var modeDesc = overwriteExisting ? "(mode forçage complet)" : "(opérations non catégorisées)";

        if (count > 0)
        {
            TempData["SuccessMessage"] = $"{count} opération(s) catégorisée(s) avec succès {modeDesc}.";
        }
        else
        {
            TempData["InfoMessage"] = "Aucune opération mise à jour. Les opérations correspondent déjà aux règles ou aucune nouvelle règle ne s'applique.";
        }

        return RedirectToPage(new { id });
    }
}
