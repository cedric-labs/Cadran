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
public class TransactionsModel(
    AppDbContext dbContext,
    ITransactionStatusService statusService,
    IUiSettingsService uiSettingsService,
    IRulesEngineService rulesEngineService,
    IMerchantService merchantService,
    Ledger.Features.Recurring.Services.IRecurringExpenseService recurringExpenseService,
    Ledger.Infrastructure.Configuration.ILocalizedEntityService localizedEntityService,
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
    public override string DefaultTableKey => "Transactions";

    protected override string GetDefaultRedirectUrl(Transaction? transaction = null)
    {
        return "/Transactions";
    }

    [BindProperty(SupportsGet = true)]
    public Guid? AccountId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool AnomalyOnly { get; set; }

    public int SalaryMonthStartDay { get; set; } = 1;

    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal SalaryCredits { get; set; }
    public decimal OtherCredits { get; set; }
    public decimal NetTotal => TotalCredits - TotalDebits;
    public int TotalCount { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / (EffectivePageSize > 0 ? EffectivePageSize : 50));
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int StartItemIndex => TotalCount == 0 ? 0 : (PageNumber - 1) * EffectivePageSize + 1;
    public int EndItemIndex => Math.Min(PageNumber * EffectivePageSize, TotalCount);

    public bool HasActiveFilters =>
        HasBaseActiveFilters ||
        (AccountId.HasValue && AccountId.Value != Guid.Empty) ||
        AnomalyOnly;

    public int ActiveFilterCriteriaCount
    {
        get
        {
            int count = BaseFilterCriteriaCount;
            if (AccountId.HasValue && AccountId.Value != Guid.Empty) count++;
            if (AnomalyOnly) count++;
            return count;
        }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var defaultPeriod = await UiSettingsService.GetDefaultPeriodAsync(cancellationToken);
        var defaultPageSize = await UiSettingsService.GetTransactionsPageSizeAsync(cancellationToken);
        int fallbackPageSize = defaultPageSize > 0 ? defaultPageSize : 50;
        EffectivePageSize = PageSize is > 0 ? PageSize.Value : fallbackPageSize;
        MaxTransactionsLimit = EffectivePageSize;
        ShowKpiCents = await UiSettingsService.GetShowKpiCentsAsync(cancellationToken);
        SalaryMonthStartDay = await UiSettingsService.GetSalaryMonthStartDayAsync(cancellationToken);

        var resolvedPeriod = BudgetPeriodCalculator.ResolvePeriod(Period, StartDate, EndDate, defaultPeriod, SalaryMonthStartDay);
        StartDate = resolvedPeriod.StartDate;
        EndDate = resolvedPeriod.EndDate;
        Period = resolvedPeriod.Period;

        await LoadReferenceSelectorsAsync(DefaultTableKey, cancellationToken: cancellationToken);

        AvailableAccounts = await DbContext.BankAccounts
            .Include(a => a.Bank)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        var query = DbContext.Transactions
            .Include($"{nameof(Transaction.BankAccount)}.{nameof(BankAccount.Bank)}")
            .Include(t => t.Category)
            .Include(t => t.CustomStatus)
            .Include(t => t.Merchant)
            .Include(t => t.Splits)
                .ThenInclude(s => s.Category)
            .Include(t => t.Splits)
                .ThenInclude(s => s.Merchant)
            .AsQueryable();

        if (AccountId.HasValue && AccountId.Value != Guid.Empty)
        {
            query = query.Where(t => t.BankAccountId == AccountId.Value);
        }

        query = await ApplyCommonTransactionFiltersAsync(query, cancellationToken);

        if (AnomalyOnly)
        {
            query = query.Where(t => t.Category != null && (
                (t.Type == TransactionType.Debit && t.Category.IsIncome) ||
                (t.Type == TransactionType.Credit && !t.Category.IsIncome)
            ));
        }

        var allFiltered = await query.ToListAsync(cancellationToken);
        allFiltered = await ApplyPostEvaluationFiltersAsync(allFiltered, cancellationToken);

        TotalCount = allFiltered.Count;
        TotalDebits = allFiltered.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);
        TotalCredits = allFiltered.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
        SalaryCredits = allFiltered
            .Where(t => t.Type == TransactionType.Credit && t.Category != null && t.Category.IsSalary)
            .Sum(t => t.Amount);
        OtherCredits = TotalCredits - SalaryCredits;

        await PaginateAndLoadMatchesAsync(allFiltered, cancellationToken);

        return Page();
    }

    public string GetPageUrl(int page)
    {
        var qs = BuildPageQueryString(page);
        return $"/Transactions?{qs}";
    }
}
