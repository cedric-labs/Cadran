using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Merchants.Services;
using Ledger.Features.RulesEngine.Entities;
using Ledger.Features.RulesEngine.Services;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Accounts.Pages;

public abstract class TransactionOperationsPageModelBase(
    AppDbContext dbContext,
    ITransactionStatusService statusService,
    IUiSettingsService uiSettingsService,
    IRulesEngineService rulesEngineService,
    IMerchantService merchantService,
    Ledger.Features.Recurring.Services.IRecurringExpenseService recurringExpenseService,
    ILocalizedEntityService localizedEntityService) : PageModel
{
    protected readonly AppDbContext DbContext = dbContext;
    protected readonly ITransactionStatusService StatusService = statusService;
    protected readonly IUiSettingsService UiSettingsService = uiSettingsService;
    protected readonly IRulesEngineService RulesEngineService = rulesEngineService;
    protected readonly IMerchantService MerchantService = merchantService;
    protected readonly Ledger.Features.Recurring.Services.IRecurringExpenseService RecurringExpenseService = recurringExpenseService;
    protected readonly ILocalizedEntityService LocalizedEntityService = localizedEntityService;

    public abstract string DefaultTableKey { get; }
    protected abstract string GetDefaultRedirectUrl(Transaction? transaction = null);

    public string? TableColumnsConfig { get; set; }
    public Dictionary<Guid, string> CategoryTranslations { get; set; } = new();
    public Dictionary<Guid, string> StatusTranslations { get; set; } = new();
    public List<Merchant> AvailableMerchants { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Period { get; set; }

    [BindProperty(SupportsGet = true)]
    public TransactionType? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AmountOperator { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? AmountValue { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? AmountMaxValue { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RuleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RecurringFilter { get; set; }

    public List<Category> AvailableCategories { get; set; } = new();
    public List<Category> ParentCategories { get; set; } = new();
    public List<BankAccount> AvailableAccounts { get; set; } = new();
    public List<Ledger.Features.Recurring.Entities.RecurringExpense> AvailableRecurringExpenses { get; set; } = new();
    public List<TransactionCustomStatus> AvailableStatuses { get; set; } = new();

    public IReadOnlyList<Transaction> Transactions { get; set; } = new List<Transaction>();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int? PageSize { get; set; }

    public int EffectivePageSize { get; set; } = 50;
    public int MaxTransactionsLimit { get; set; } = 250;
    public bool ShowKpiCents { get; set; } = true;
    public Dictionary<Guid, DetailedRuleMatch> RuleMatches { get; set; } = new();
    public Dictionary<Guid, Ledger.Features.Recurring.Models.RecurringMatchDto> RecurringMatches { get; set; } = new();

    protected bool HasBaseActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm) ||
        CategoryId.HasValue ||
        StartDate.HasValue ||
        EndDate.HasValue ||
        Type.HasValue ||
        (AmountValue.HasValue && !string.IsNullOrWhiteSpace(AmountOperator)) ||
        (!string.IsNullOrWhiteSpace(RuleFilter) && RuleFilter != "all") ||
        (!string.IsNullOrWhiteSpace(RecurringFilter) && RecurringFilter != "all");

    protected int BaseFilterCriteriaCount
    {
        get
        {
            int count = 0;
            if (CategoryId.HasValue) count++;
            if (Type.HasValue) count++;
            if (StartDate.HasValue || EndDate.HasValue) count++;
            if (AmountValue.HasValue && !string.IsNullOrWhiteSpace(AmountOperator)) count++;
            if (!string.IsNullOrWhiteSpace(RuleFilter) && RuleFilter != "all") count++;
            if (!string.IsNullOrWhiteSpace(RecurringFilter) && RecurringFilter != "all") count++;
            return count;
        }
    }

    protected async Task LoadReferenceSelectorsAsync(string tableColumnsKey, Guid? accountFilterForRecurring = null, CancellationToken cancellationToken = default)
    {
        AvailableStatuses = await StatusService.GetAllStatusesAsync(cancellationToken) ?? new();
        TableColumnsConfig = await UiSettingsService.GetTableColumnsConfigAsync(tableColumnsKey, cancellationToken);

        AvailableCategories = await DbContext.Categories
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        ParentCategories = await DbContext.Categories
            .Include(c => c.SubCategories)
            .Where(c => c.ParentCategoryId == null)
            .OrderBy(c => c.IsIncome)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        CategoryTranslations = await LocalizedEntityService.GetTranslationsAsync(
            "Category",
            AvailableCategories.Select(c => c.Id),
            "Name",
            cancellationToken: cancellationToken) ?? new();

        StatusTranslations = await LocalizedEntityService.GetTranslationsAsync(
            "TransactionCustomStatus",
            AvailableStatuses.Select(s => s.Id),
            "Name",
            cancellationToken: cancellationToken) ?? new();

        var recQuery = DbContext.RecurringExpenses.Where(r => r.IsActive);
        if (accountFilterForRecurring.HasValue)
        {
            recQuery = recQuery.Where(r => !r.TargetBankAccountId.HasValue || r.TargetBankAccountId.Value == accountFilterForRecurring.Value);
        }

        AvailableRecurringExpenses = await recQuery
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        AvailableMerchants = await MerchantService.GetAllMerchantsAsync(cancellationToken) ?? new();
    }

    protected async Task<IQueryable<Transaction>> ApplyCommonTransactionFiltersAsync(IQueryable<Transaction> query, CancellationToken cancellationToken = default)
    {
        if (CategoryId.HasValue)
        {
            if (CategoryId.Value == Guid.Empty)
            {
                query = query.Where(t => t.CategoryId == null);
            }
            else
            {
                var selectedCat = await DbContext.Categories
                    .Include(c => c.SubCategories)
                    .FirstOrDefaultAsync(c => c.Id == CategoryId.Value, cancellationToken);

                if (selectedCat != null && selectedCat.SubCategories.Count > 0)
                {
                    var allIds = selectedCat.SubCategories.Select(s => s.Id).ToList();
                    allIds.Add(selectedCat.Id);
                    query = query.Where(t => t.CategoryId != null && allIds.Contains(t.CategoryId.Value));
                }
                else
                {
                    query = query.Where(t => t.CategoryId == CategoryId.Value);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.Trim();
            query = query.Where(t => EF.Functions.Like(t.Label, $"%{term}%")
                                  || (t.RawLabel != null && EF.Functions.Like(t.RawLabel, $"%{term}%"))
                                  || (t.Reference != null && EF.Functions.Like(t.Reference, $"%{term}%"))
                                  || (t.Notes != null && EF.Functions.Like(t.Notes, $"%{term}%")));
        }

        if (StartDate.HasValue)
        {
            query = query.Where(t => (t.BudgetDate ?? t.Date) >= StartDate.Value.Date);
        }

        if (EndDate.HasValue)
        {
            var endOfDay = EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(t => (t.BudgetDate ?? t.Date) <= endOfDay);
        }

        if (Type.HasValue)
        {
            query = query.Where(t => t.Type == Type.Value);
        }

        if (AmountValue.HasValue && !string.IsNullOrWhiteSpace(AmountOperator))
        {
            var val = AmountValue.Value;
            switch (AmountOperator.ToLowerInvariant())
            {
                case "gt":
                    query = query.Where(t => t.Amount > val);
                    break;
                case "gte":
                    query = query.Where(t => t.Amount >= val);
                    break;
                case "lt":
                    query = query.Where(t => t.Amount < val);
                    break;
                case "lte":
                    query = query.Where(t => t.Amount <= val);
                    break;
                case "eq":
                    query = query.Where(t => t.Amount == val);
                    break;
                case "between":
                    var maxVal = AmountMaxValue ?? val;
                    var minB = Math.Min(val, maxVal);
                    var maxB = Math.Max(val, maxVal);
                    query = query.Where(t => t.Amount >= minB && t.Amount <= maxB);
                    break;
            }
        }

        return query;
    }

    protected async Task<List<Transaction>> ApplyPostEvaluationFiltersAsync(List<Transaction> allFiltered, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(RuleFilter) && RuleFilter != "all")
        {
            var allMatches = await RulesEngineService.GetMatchesForTransactionsAsync(allFiltered, cancellationToken);
            allFiltered = RuleFilter switch
            {
                "none" => allFiltered.Where(t => allMatches.TryGetValue(t.Id, out var m) && m.MatchCount == 0).ToList(),
                "single" => allFiltered.Where(t => allMatches.TryGetValue(t.Id, out var m) && m.MatchCount == 1).ToList(),
                "multiple" => allFiltered.Where(t => allMatches.TryGetValue(t.Id, out var m) && m.MatchCount > 1).ToList(),
                _ => allFiltered
            };
        }

        if (!string.IsNullOrWhiteSpace(RecurringFilter) && RecurringFilter != "all")
        {
            var allRecurringMatches = await RecurringExpenseService.GetMatchesForTransactionsAsync(allFiltered, cancellationToken);
            if (Guid.TryParse(RecurringFilter, out var targetExpenseId))
            {
                allFiltered = allFiltered.Where(t => allRecurringMatches.TryGetValue(t.Id, out var m) &&
                                                     m.MatchingExpenseIds != null &&
                                                     m.MatchingExpenseIds.Contains(targetExpenseId)).ToList();
            }
            else
            {
                allFiltered = RecurringFilter switch
                {
                    "none" => allFiltered.Where(t => allRecurringMatches.TryGetValue(t.Id, out var m) && m.MatchCount == 0).ToList(),
                    "any" => allFiltered.Where(t => allRecurringMatches.TryGetValue(t.Id, out var m) && m.MatchCount > 0).ToList(),
                    "single" => allFiltered.Where(t => allRecurringMatches.TryGetValue(t.Id, out var m) && m.MatchCount == 1).ToList(),
                    "multiple" => allFiltered.Where(t => allRecurringMatches.TryGetValue(t.Id, out var m) && m.MatchCount > 1).ToList(),
                    _ => allFiltered
                };
            }
        }

        return allFiltered;
    }

    protected async Task PaginateAndLoadMatchesAsync(List<Transaction> allFiltered, CancellationToken cancellationToken = default)
    {
        var orderedQuery = allFiltered
            .OrderByDescending(t => t.BudgetDate ?? t.Date)
            .ThenByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAtUtc);

        var totalPagesCount = (int)Math.Ceiling((double)allFiltered.Count / (EffectivePageSize > 0 ? EffectivePageSize : 50));
        if (totalPagesCount < 1) totalPagesCount = 1;
        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > totalPagesCount) PageNumber = totalPagesCount;

        Transactions = orderedQuery
            .Skip((PageNumber - 1) * EffectivePageSize)
            .Take(EffectivePageSize)
            .ToList();

        RuleMatches = await RulesEngineService.GetMatchesForTransactionsAsync(Transactions, cancellationToken);
        RecurringMatches = await RecurringExpenseService.GetMatchesForTransactionsAsync(Transactions, cancellationToken);
    }

    protected string BuildPageQueryString(int page)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (Request?.Query != null)
        {
            foreach (var q in Request.Query)
            {
                dict[q.Key] = q.Value.ToString();
            }
        }
        dict["PageNumber"] = page.ToString();
        dict["PageSize"] = EffectivePageSize.ToString();

        return string.Join("&", dict
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value ?? string.Empty)}"));
    }

    public async Task<IActionResult> OnPostUpdateCategoryAsync(Guid transactionId,
        Guid? newCategoryId,
        DateTime? budgetDate = null,
        string? notes = null,
        Guid? statusId = null,
        TransactionStatus? status = null,
        bool applyToSimilarLabels = false,
        Guid? merchantId = null,
        string? newMerchantName = null, CancellationToken cancellationToken = default)
    {
        var transaction = await DbContext.Transactions
            .Include(t => t.CustomStatus)
            .Include(t => t.Merchant)
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        if (transaction == null)
        {
            return NotFound();
        }

        Guid? targetCategoryId = (newCategoryId.HasValue && newCategoryId.Value != Guid.Empty)
            ? newCategoryId.Value
            : null;

        Category? updatedCategory = null;
        if (targetCategoryId.HasValue)
        {
            updatedCategory = await DbContext.Categories.FindAsync([targetCategoryId.Value], cancellationToken);
            if (updatedCategory == null)
            {
                return BadRequest("Catégorie introuvable.");
            }
        }

        Guid? targetMerchantId = merchantId;
        if (!string.IsNullOrWhiteSpace(newMerchantName))
        {
            var createdMerchant = await MerchantService.GetOrCreateByNameAsync(newMerchantName.Trim(), cancellationToken: cancellationToken);
            targetMerchantId = createdMerchant.Id;
        }
        else if (merchantId.HasValue && merchantId.Value == Guid.Empty)
        {
            targetMerchantId = null;
        }

        Merchant? updatedMerchant = null;
        if (targetMerchantId.HasValue)
        {
            updatedMerchant = await MerchantService.GetByIdAsync(targetMerchantId.Value, cancellationToken);
        }

        transaction.BudgetDate = budgetDate;
        transaction.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (statusId.HasValue && statusId.Value != Guid.Empty)
        {
            var customStatus = await StatusService.GetByIdAsync(statusId.Value, cancellationToken);
            if (customStatus != null)
            {
                transaction.CustomStatusId = customStatus.Id;
                transaction.CustomStatus = customStatus;
                if (!string.IsNullOrEmpty(customStatus.Code) && Enum.TryParse<TransactionStatus>(customStatus.Code, out var enumStatus))
                {
                    transaction.Status = enumStatus;
                }
            }
        }
        else if (status.HasValue)
        {
            transaction.Status = status.Value;
        }

        if (applyToSimilarLabels && !string.IsNullOrWhiteSpace(transaction.Label))
        {
            var label = transaction.Label;
            var similarTransactions = await DbContext.Transactions
                .Where(t => t.Label == label)
                .ToListAsync(cancellationToken);

            foreach (var t in similarTransactions)
            {
                t.CategoryId = targetCategoryId;
                t.MerchantId = targetMerchantId;
            }
        }
        else
        {
            transaction.CategoryId = targetCategoryId;
            transaction.MerchantId = targetMerchantId;
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        if (IsJsonRequest())
        {
            var isShifted = transaction.BudgetDate.HasValue &&
                (transaction.BudgetDate.Value.Year != transaction.Date.Year || transaction.BudgetDate.Value.Month != transaction.Date.Month);

            return new JsonResult(new
            {
                success = true,
                categoryId = targetCategoryId,
                categoryName = updatedCategory?.Name ?? "Non catégorisé",
                categoryIcon = updatedCategory?.IconName ?? "bi-tag",
                categoryColor = updatedCategory?.ColorHex ?? "#6c757d",
                merchantId = targetMerchantId,
                merchantName = updatedMerchant?.Name ?? "",
                budgetDate = transaction.BudgetDate?.ToString("yyyy-MM-dd"),
                budgetDateStr = transaction.BudgetDate?.ToString("yyyy-MM-dd"),
                isShifted,
                budgetBadgeText = (isShifted && transaction.BudgetDate.HasValue) ? transaction.BudgetDate.Value.ToString("MM/yyyy") : null,
                notes = transaction.Notes,
                statusId = transaction.CustomStatusId,
                statusDisplayName = transaction.GetStatusDisplayName(),
                statusColorHex = transaction.GetStatusColorHex(),
                statusIconClass = transaction.GetStatusIconClass()
            });
        }

        return RedirectToRefererOrDefault(transaction);
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(Guid transactionId, Guid statusId, CancellationToken cancellationToken = default)
    {
        var transaction = await DbContext.Transactions
            .Include(t => t.CustomStatus)
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

        if (transaction == null)
        {
            return NotFound();
        }

        var customStatus = await StatusService.GetByIdAsync(statusId, cancellationToken);
        if (customStatus == null)
        {
            return BadRequest("Statut introuvable.");
        }

        transaction.CustomStatusId = customStatus.Id;
        transaction.CustomStatus = customStatus;
        if (!string.IsNullOrEmpty(customStatus.Code) && Enum.TryParse<TransactionStatus>(customStatus.Code, out var enumStatus))
        {
            transaction.Status = enumStatus;
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        if (IsJsonRequest())
        {
            return new JsonResult(new
            {
                success = true,
                transactionId = transaction.Id,
                statusId = customStatus.Id,
                displayName = customStatus.Name,
                colorHex = customStatus.ColorHex,
                iconClass = customStatus.IconName
            });
        }

        return RedirectToRefererOrDefault(transaction);
    }

    public async Task<IActionResult> OnPostBulkUpdateStatusAsync(List<Guid> transactionIds, Guid statusId, CancellationToken cancellationToken = default)
    {
        if (transactionIds == null || transactionIds.Count == 0)
        {
            return BadRequest("Aucune opération sélectionnée.");
        }

        var customStatus = await StatusService.GetByIdAsync(statusId, cancellationToken);
        if (customStatus == null)
        {
            return BadRequest("Statut introuvable.");
        }

        var transactions = await DbContext.Transactions
            .Where(t => transactionIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        foreach (var t in transactions)
        {
            t.CustomStatusId = customStatus.Id;
            if (!string.IsNullOrEmpty(customStatus.Code) && Enum.TryParse<TransactionStatus>(customStatus.Code, out var enumStatus))
            {
                t.Status = enumStatus;
            }
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        if (IsJsonRequest())
        {
            return new JsonResult(new
            {
                success = true,
                count = transactions.Count,
                statusId = customStatus.Id,
                displayName = customStatus.Name,
                colorHex = customStatus.ColorHex,
                iconClass = customStatus.IconName,
                updatedIds = transactions.Select(t => t.Id).ToList()
            });
        }

        TempData["SuccessMessage"] = $"{transactions.Count} opération(s) mise(s) à jour avec le statut '{customStatus.Name}'.";
        return RedirectToRefererOrDefault();
    }

    public async Task<IActionResult> OnPostSaveColumnsConfigAsync([FromBody] ColumnConfigDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.ConfigJson))
        {
            return BadRequest("Configuration invalide.");
        }

        await UiSettingsService.SetTableColumnsConfigAsync(dto.TableKey ?? DefaultTableKey, dto.ConfigJson, cancellationToken);
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostCreateRuleFromTransactionAsync([FromBody] QuickRuleDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Pattern) || dto.TargetCategoryId == Guid.Empty)
        {
            return BadRequest("Données de la règle incomplètes.");
        }

        try
        {
            _ = new System.Text.RegularExpressions.Regex(dto.Pattern.Trim(), System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
        }
        catch (ArgumentException)
        {
            // La syntaxe regex invalide est interceptée pour retourner une erreur 400 claire au client
            return BadRequest("Le motif de règle généré est invalide.");
        }

        Guid? targetMerchantId = dto.TargetMerchantId;
        if (!targetMerchantId.HasValue && !string.IsNullOrWhiteSpace(dto.NewMerchantName))
        {
            var createdMerchant = await MerchantService.GetOrCreateByNameAsync(dto.NewMerchantName.Trim(), cancellationToken: cancellationToken);
            targetMerchantId = createdMerchant.Id;
        }

        var rule = new CategorizationRule
        {
            UserId = string.Empty,
            Name = dto.Name.Trim(),
            Pattern = dto.Pattern.Trim(),
            TargetCategoryId = dto.TargetCategoryId,
            TargetBankAccountId = dto.TargetBankAccountId,
            TargetMerchantId = targetMerchantId,
            Priority = dto.Priority > 0 ? dto.Priority : 10,
            ShiftToNextMonth = dto.ShiftToNextMonth,
            IsActive = true
        };

        DbContext.CategorizationRules.Add(rule);
        await DbContext.SaveChangesAsync(cancellationToken);

        int appliedCount = 0;
        if (dto.ApplyImmediately)
        {
            appliedCount = await RulesEngineService.ApplyRulesToDatabaseAsync(
                overwriteExisting: dto.OverwriteExisting,
                bankAccountId: dto.TargetBankAccountId,
                singleRuleId: rule.Id,
                cancellationToken: cancellationToken);
        }

        return new JsonResult(new
        {
            success = true,
            ruleId = rule.Id,
            ruleName = rule.Name,
            appliedCount
        });
    }

    public async Task<IActionResult> OnPostDeleteTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var tx = await DbContext.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        if (tx == null)
        {
            return NotFound(new { success = false, message = "Opération non trouvée." });
        }

        DbContext.Transactions.Remove(tx);
        await DbContext.SaveChangesAsync(cancellationToken);

        if (IsJsonRequest())
        {
            return new JsonResult(new { success = true, transactionId });
        }

        TempData["SuccessMessage"] = "Opération supprimée avec succès.";
        return RedirectToRefererOrDefault();
    }

    public async Task<IActionResult> OnPostBulkDeleteTransactionsAsync([FromForm] List<Guid> transactionIds, CancellationToken cancellationToken = default)
    {
        if (transactionIds == null || transactionIds.Count == 0)
        {
            return BadRequest(new { success = false, message = "Aucune opération spécifiée." });
        }

        var txs = await DbContext.Transactions
            .Where(t => transactionIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        var count = txs.Count;
        DbContext.Transactions.RemoveRange(txs);
        await DbContext.SaveChangesAsync(cancellationToken);

        if (IsJsonRequest())
        {
            return new JsonResult(new { success = true, deletedCount = count, deletedIds = transactionIds });
        }

        TempData["SuccessMessage"] = $"{count} opération(s) supprimée(s) avec succès.";
        return RedirectToRefererOrDefault();
    }

    public async Task<IActionResult> OnPostCreateRecurringFromTransactionAsync([FromBody] CreateRecurringDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Pattern) || dto.ExpectedAmount <= 0)
        {
            return BadRequest("Données incomplètes pour créer la charge récurrente.");
        }

        var candidate = new Ledger.Features.Recurring.Models.RecurringCandidateDto
        {
            Pattern = dto.Pattern.Trim(),
            SuggestedName = dto.Name.Trim(),
            AverageAmount = dto.ExpectedAmount,
            LastAmount = dto.ExpectedAmount,
            ExpectedDayOfMonth = dto.ExpectedDayOfMonth,
            Frequency = dto.Frequency,
            SuggestedCategoryId = dto.TargetCategoryId,
            BankAccountId = dto.TargetBankAccountId,
            LastDate = DateTime.Today
        };

        var created = await RecurringExpenseService.ConfirmCandidateAsync(candidate, cancellationToken);

        if (dto.AmountTolerancePercentage.HasValue || !string.IsNullOrWhiteSpace(dto.Notes))
        {
            created.AmountTolerancePercentage = dto.AmountTolerancePercentage ?? 0.05m;
            if (!string.IsNullOrWhiteSpace(dto.Notes))
            {
                created.Notes = dto.Notes.Trim();
            }
            await RecurringExpenseService.UpdateAsync(created, cancellationToken);
        }

        return new JsonResult(new
        {
            success = true,
            expenseId = created.Id,
            expenseName = created.Name
        });
    }

    public async Task<IActionResult> OnGetTransactionSplitsAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var tx = await DbContext.Transactions
            .Include(t => t.Splits)
            .Include(t => t.BankAccount)
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

        if (tx == null) return NotFound(new { success = false, message = "Opération introuvable." });

        var splits = tx.Splits
            .OrderBy(s => s.CreatedAtUtc)
            .Select(s => new
            {
                id = s.Id,
                amount = s.Amount,
                categoryId = s.CategoryId,
                merchantId = s.MerchantId,
                notes = s.Notes
            })
            .ToList();

        var currencySymbol = CurrencyService.GetStaticSymbol(tx.BankAccount?.Currency);

        return new JsonResult(new
        {
            success = true,
            transactionId = tx.Id,
            label = tx.Label,
            totalAmount = tx.Amount,
            type = tx.Type,
            date = tx.Date,
            isSplit = tx.IsSplit,
            currencySymbol,
            splits
        });
    }

    public async Task<IActionResult> OnPostSaveTransactionSplitsAsync([FromBody] SaveSplitsDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null) return BadRequest(new { success = false, message = "Données invalides." });

        var tx = await DbContext.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == dto.TransactionId, cancellationToken);

        if (tx == null) return NotFound(new { success = false, message = "Opération introuvable." });

        if (dto.Splits == null || dto.Splits.Count < 2)
        {
            return BadRequest(new { success = false, message = "Une ventilation doit comporter au moins 2 parts." });
        }

        var splitsSum = dto.Splits.Sum(s => s.Amount);
        if (Math.Abs(splitsSum - tx.Amount) > 0.01m)
        {
            return BadRequest(new { success = false, message = $"La somme des parts ({splitsSum:N2} €) doit correspondre au montant total de l'opération ({tx.Amount:N2} €)." });
        }

        DbContext.TransactionSplits.RemoveRange(tx.Splits);

        foreach (var s in dto.Splits)
        {
            var split = new TransactionSplit
            {
                Id = Guid.NewGuid(),
                UserId = tx.UserId,
                TransactionId = tx.Id,
                Amount = Math.Abs(s.Amount),
                CategoryId = s.CategoryId,
                MerchantId = s.MerchantId,
                Notes = s.Notes?.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };
            DbContext.TransactionSplits.Add(split);
        }

        tx.IsSplit = true;
        tx.UpdatedAtUtc = DateTime.UtcNow;
        await DbContext.SaveChangesAsync(cancellationToken);

        return new JsonResult(new { success = true, splitsCount = dto.Splits.Count });
    }

    public async Task<IActionResult> OnPostRemoveTransactionSplitsAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var tx = await DbContext.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

        if (tx == null) return NotFound(new { success = false, message = "Opération introuvable." });

        DbContext.TransactionSplits.RemoveRange(tx.Splits);
        tx.IsSplit = false;
        tx.UpdatedAtUtc = DateTime.UtcNow;
        await DbContext.SaveChangesAsync(cancellationToken);

        return new JsonResult(new { success = true });
    }

    private bool IsJsonRequest()
    {
        return Request.Headers.XRequestedWith == "XMLHttpRequest" ||
               Request.Headers.Accept.ToString().Contains("application/json");
    }

    private RedirectResult RedirectToRefererOrDefault(Transaction? transaction = null)
    {
        var referer = Request.Headers.Referer.ToString();
        return Redirect(!string.IsNullOrEmpty(referer) ? referer : GetDefaultRedirectUrl(transaction));
    }

    public class ColumnConfigDto
    {
        public string? TableKey { get; set; }
        public string? ConfigJson { get; set; }
    }

    public class CreateRecurringDto
    {
        public string Name { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public decimal ExpectedAmount { get; set; }
        public int? ExpectedDayOfMonth { get; set; }
        public Ledger.Features.Recurring.Entities.RecurringFrequency Frequency { get; set; } = Ledger.Features.Recurring.Entities.RecurringFrequency.Monthly;
        public decimal? AmountTolerancePercentage { get; set; } = 0.05m;
        public Guid? TargetCategoryId { get; set; }
        public Guid? TargetBankAccountId { get; set; }
        public string? Notes { get; set; }
    }

    public class QuickRuleDto
    {
        public string Name { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public Guid TargetCategoryId { get; set; }
        public Guid? TargetBankAccountId { get; set; }
        public Guid? TargetMerchantId { get; set; }
        public string? NewMerchantName { get; set; }
        public int Priority { get; set; } = 10;
        public bool ShiftToNextMonth { get; set; }
        public bool ApplyImmediately { get; set; } = true;
        public bool OverwriteExisting { get; set; } = false;
    }

    public class SaveSplitsDto
    {
        public Guid TransactionId { get; set; }
        public List<SplitLineDto> Splits { get; set; } = new();
    }

    public class SplitLineDto
    {
        public decimal Amount { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? MerchantId { get; set; }
        public string? Notes { get; set; }
    }
}
