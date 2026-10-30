using System.Globalization;
using System.Text.Json;
using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Services;
using Ledger.Features.Reports.Entities;
using Ledger.Features.Reports.Models;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Service d'orchestration pour le calcul, la sauvegarde et l'exploration détaillée des rapports tableaux croisés dynamiques.
/// </summary>
public sealed class PivotReportService(
    AppDbContext dbContext,
    ILocalizedEntityService localizedEntityService,
    IStringLocalizer localizer,
    IUiSettingsService uiSettingsService,
    TimeProvider timeProvider,
    IPivotAggregationCalculator? aggregationCalculator = null) : IPivotReportService
{
    private readonly IPivotAggregationCalculator _aggregationCalculator = aggregationCalculator ?? new PivotAggregationCalculator(localizer);

    public async Task<PivotResultDto> ComputePivotAsync(PivotQueryDto query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = new PivotResultDto { Query = query };

        // 1. Calculer la plage de dates effective
        var (start, end) = await ResolvePeriodDatesAsync(query.PeriodPreset, query.StartDate, query.EndDate).ConfigureAwait(false);
        result.EffectiveStartDate = start;
        result.EffectiveEndDate = end;

        // 2. Construire la liste ordonnée des colonnes
        result.Columns = _aggregationCalculator.BuildColumnHeaders(start, end, query.ColumnDimension);

        // 3. Charger les charges récurrentes si besoin pour le filtrage ou détection
        var recurringRules = await LoadRecurringRulesAsync(query.FlowFilter, cancellationToken).ConfigureAwait(false);

        // 4. Charger les catégories et comptes pour les libellés/hiérarchies
        var categories = await dbContext.Categories.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var accounts = await dbContext.BankAccounts.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var categoryNameOverrides = await LoadCategoryTranslationsAsync(categories, cancellationToken).ConfigureAwait(false);

        // 5. Requête sur les transactions
        var flatItems = await LoadFlatTransactionsAsync(query, start, end, categories, recurringRules, cancellationToken).ConfigureAwait(false);
        result.TotalTransactionsCount = flatItems.Count;

        // 6. Agréger les données par ligne et colonne
        result.Rows = _aggregationCalculator.AggregateRows(flatItems, result.Columns, categories, accounts, categoryNameOverrides, query);

        // 7. Calculer les totaux de colonnes, grand total et pourcentages
        _aggregationCalculator.CalculateTotalsAndPercentages(result);

        return result;
    }

    public Task<List<SavedPivotReport>> GetSavedPivotReportsAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SavedPivotReports
            .OrderByDescending(r => r.IsDefault)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<SavedPivotReport?> GetSavedPivotReportByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.SavedPivotReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<SavedPivotReport> SavePivotReportAsync(
        string name,
        string? description,
        PivotQueryDto query,
        Guid? existingId = null,
        bool isDefault = false,
        CancellationToken cancellationToken = default)
    {
        var configJson = JsonSerializer.Serialize(query);

        if (isDefault)
        {
            var existingDefaults = await dbContext.SavedPivotReports.Where(r => r.IsDefault).ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var d in existingDefaults)
            {
                d.IsDefault = false;
            }
        }

        SavedPivotReport report;
        if (existingId.HasValue)
        {
            var existing = await dbContext.SavedPivotReports.FirstOrDefaultAsync(r => r.Id == existingId.Value, cancellationToken).ConfigureAwait(false);
            if (existing != null)
            {
                existing.Name = name.Trim();
                existing.Description = description?.Trim();
                existing.ConfigJson = configJson;
                existing.IsDefault = isDefault;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return existing;
            }
        }

        report = new SavedPivotReport
        {
            UserId = string.Empty, // Injecté automatiquement par SaveChangesAsync
            Name = name.Trim(),
            Description = description?.Trim(),
            ConfigJson = configJson,
            IsDefault = isDefault
        };

        dbContext.SavedPivotReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return report;
    }

    public async Task<bool> DeleteSavedPivotReportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.SavedPivotReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        if (item == null)
        {
            return false;
        }

        dbContext.SavedPivotReports.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<PivotCellDetailDto> GetCellDetailAsync(
        PivotQueryDto query,
        string rowKey,
        string colKey,
        CancellationToken cancellationToken = default)
    {
        var detail = new PivotCellDetailDto();

        // 1. Calculer la plage de dates globale de la requête
        var (queryStart, queryEnd) = await ResolvePeriodDatesAsync(query.PeriodPreset, query.StartDate, query.EndDate).ConfigureAwait(false);

        // 2. Déterminer la plage de dates de la colonne cliquée
        var (colStart, colEnd, colDisplayLabel) = ResolveColumnDateRange(query, colKey, queryStart, queryEnd);
        detail.ColumnLabel = colDisplayLabel;

        // 3. Charger les catégories, comptes et charges récurrentes
        var categories = await dbContext.Categories.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var categoryMap = categories.ToDictionary(c => c.Id);
        var categoryTranslations = await LoadCategoryTranslationsAsync(categories, cancellationToken).ConfigureAwait(false);
        var recurringRules = await LoadRecurringRulesAsync(query.FlowFilter, cancellationToken).ConfigureAwait(false);

        // 4. Charger et aplatir les transactions
        var flatItems = await LoadFlatTransactionsAsync(query, colStart, colEnd, categories, recurringRules, cancellationToken).ConfigureAwait(false);

        // 5. Filtrer par rowKey
        var (filteredItems, rowDisplayLabel) = FilterTransactionsByRowKey(flatItems, rowKey, query.RowDimension, categoryMap, categoryTranslations);

        filteredItems = filteredItems
            .OrderByDescending(t => t.BudgetDate ?? t.Date)
            .ThenByDescending(t => t.CreatedAtUtc)
            .ToList();

        detail.RowLabel = rowDisplayLabel;
        detail.TotalAmount = filteredItems.Sum(t => t.Type == TransactionType.Debit ? -Math.Abs(t.Amount) : Math.Abs(t.Amount));
        detail.TransactionsCount = filteredItems.Count;

        detail.Transactions = filteredItems.Select(t =>
        {
            string cName = "Sans catégorie";
            string? cColor = null;
            string? cIcon = null;
            bool isCatIncome = false;
            bool isAnomaly = false;

            if (t.CategoryId.HasValue && categoryMap.TryGetValue(t.CategoryId.Value, out var c))
            {
                cName = categoryTranslations.TryGetValue(c.Id, out var tr) && !string.IsNullOrWhiteSpace(tr) ? tr : c.Name;
                cColor = c.ColorHex;
                cIcon = c.IconName;
                isCatIncome = c.IsIncome;
                if ((t.Type == TransactionType.Debit && c.IsIncome) || (t.Type == TransactionType.Credit && !c.IsIncome))
                {
                    isAnomaly = true;
                }
            }

            var signedAmount = t.Type == TransactionType.Debit ? -Math.Abs(t.Amount) : Math.Abs(t.Amount);

            return new PivotCellTransactionItemDto
            {
                Id = t.Id,
                Date = t.Date,
                BudgetDate = t.BudgetDate,
                BankAccountName = t.BankAccount?.Name ?? "Compte",
                BankName = t.BankAccount?.Bank?.Name,
                Currency = t.BankAccount?.Currency,
                Label = t.Label,
                RawLabel = t.RawLabel,
                Notes = t.Notes,
                CategoryName = cName,
                CategoryColorHex = cColor,
                CategoryIconName = cIcon,
                Type = t.Type,
                Amount = signedAmount,
                IsCategoryIncome = isCatIncome,
                IsAnomaly = isAnomaly
            };
        }).ToList();

        return detail;
    }

    private async Task<List<RecurringExpense>?> LoadRecurringRulesAsync(PivotFlowFilter flowFilter, CancellationToken cancellationToken)
    {
        if (flowFilter == PivotFlowFilter.RecurringOnly || flowFilter == PivotFlowFilter.NonRecurringDebitsOnly)
        {
            return await dbContext.RecurringExpenses
                .Where(r => r.IsActive)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<Dictionary<Guid, string>> LoadCategoryTranslationsAsync(List<Category> categories, CancellationToken cancellationToken)
    {
        var catIds = categories.Select(c => c.Id).ToList();
        var categoryTranslations = await localizedEntityService.GetTranslationsAsync(
            nameof(Category), catIds, nameof(Category.Name), cancellationToken: cancellationToken).ConfigureAwait(false);

        var categoryNameOverrides = new Dictionary<Guid, string>();
        foreach (var c in categories)
        {
            categoryNameOverrides[c.Id] = categoryTranslations.TryGetValue(c.Id, out var tr) && !string.IsNullOrWhiteSpace(tr) ? tr : c.Name;
        }

        return categoryNameOverrides;
    }

    private async Task<List<PivotTransactionItem>> LoadFlatTransactionsAsync(
        PivotQueryDto query,
        DateTime start,
        DateTime end,
        List<Category> categories,
        List<RecurringExpense>? recurringRules,
        CancellationToken cancellationToken)
    {
        var txQuery = dbContext.Transactions
            .AsNoTracking()
            .Include($"{nameof(Transaction.BankAccount)}.{nameof(BankAccount.Bank)}")
            .Include(t => t.Merchant)
            .Include(t => t.Splits).ThenInclude(s => s.Category)
            .Include(t => t.Splits).ThenInclude(s => s.Merchant)
            .Where(t => (t.BudgetDate ?? t.Date) >= start && (t.BudgetDate ?? t.Date) <= end);

        if (query.SelectedAccountIds != null && query.SelectedAccountIds.Count > 0)
        {
            txQuery = txQuery.Where(t => query.SelectedAccountIds.Contains(t.BankAccountId));
        }

        if (query.SelectedCategoryIds != null && query.SelectedCategoryIds.Count > 0)
        {
            var expandedCategoryIds = new HashSet<Guid>(query.SelectedCategoryIds);
            foreach (var cat in categories.Where(c => c.ParentCategoryId.HasValue && query.SelectedCategoryIds.Contains(c.ParentCategoryId.Value)))
            {
                expandedCategoryIds.Add(cat.Id);
            }

            txQuery = txQuery.Where(t =>
                (!t.IsSplit && t.CategoryId.HasValue && expandedCategoryIds.Contains(t.CategoryId.Value)) ||
                (t.IsSplit && t.Splits.Any(s => s.CategoryId.HasValue && expandedCategoryIds.Contains(s.CategoryId.Value))));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            txQuery = txQuery.Where(t => t.Label.Contains(term) || (t.RawLabel != null && t.RawLabel.Contains(term)));
        }

        if (query.FlowFilter == PivotFlowFilter.DebitsOnly || query.FlowFilter == PivotFlowFilter.NonRecurringDebitsOnly)
        {
            txQuery = txQuery.Where(t => t.Type == TransactionType.Debit);
        }
        else if (query.FlowFilter == PivotFlowFilter.CreditsOnly)
        {
            txQuery = txQuery.Where(t => t.Type == TransactionType.Credit);
        }

        var transactions = await txQuery.ToListAsync(cancellationToken).ConfigureAwait(false);

        if (recurringRules != null && recurringRules.Count > 0)
        {
            if (query.FlowFilter == PivotFlowFilter.RecurringOnly)
            {
                transactions = transactions.Where(t => MatchesAnyRecurring(t, recurringRules)).ToList();
            }
            else if (query.FlowFilter == PivotFlowFilter.NonRecurringDebitsOnly)
            {
                transactions = transactions.Where(t => !MatchesAnyRecurring(t, recurringRules)).ToList();
            }
        }

        var flatItems = FlattenTransactions(transactions);

        if (query.SelectedCategoryIds != null && query.SelectedCategoryIds.Count > 0)
        {
            var expandedCategoryIds = new HashSet<Guid>(query.SelectedCategoryIds);
            foreach (var cat in categories.Where(c => c.ParentCategoryId.HasValue && query.SelectedCategoryIds.Contains(c.ParentCategoryId.Value)))
            {
                expandedCategoryIds.Add(cat.Id);
            }
            flatItems = flatItems.Where(item => item.CategoryId.HasValue && expandedCategoryIds.Contains(item.CategoryId.Value)).ToList();
        }

        return flatItems;
    }

    private static (DateTime ColStart, DateTime ColEnd, string ColDisplayLabel) ResolveColumnDateRange(
        PivotQueryDto query,
        string colKey,
        DateTime queryStart,
        DateTime queryEnd)
    {
        DateTime colStart = queryStart;
        DateTime colEnd = queryEnd;
        string colDisplayLabel = "Total";

        if (!string.Equals(colKey, "TOTAL", StringComparison.OrdinalIgnoreCase))
        {
            if (query.ColumnDimension == PivotColumnDimension.Month && DateTime.TryParseExact(colKey, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
            {
                colStart = new DateTime(monthDate.Year, monthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                colEnd = colStart.AddMonths(1).AddDays(-1);
                colDisplayLabel = colStart.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
                colDisplayLabel = char.ToUpper(colDisplayLabel[0], CultureInfo.CurrentCulture) + colDisplayLabel[1..];
            }
            else if (query.ColumnDimension == PivotColumnDimension.Quarter && colKey.Contains("-Q", StringComparison.Ordinal))
            {
                var parts = colKey.Split("-Q");
                if (parts.Length == 2 && int.TryParse(parts[0], CultureInfo.InvariantCulture, out var year) && int.TryParse(parts[1], CultureInfo.InvariantCulture, out var q))
                {
                    int qStartMonth = (q - 1) * 3 + 1;
                    colStart = new DateTime(year, qStartMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                    colEnd = colStart.AddMonths(3).AddDays(-1);
                    colDisplayLabel = $"T{q} {year}";
                }
            }
            else if (query.ColumnDimension == PivotColumnDimension.Year && int.TryParse(colKey, CultureInfo.InvariantCulture, out var yr))
            {
                colStart = new DateTime(yr, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                colEnd = new DateTime(yr, 12, 31, 0, 0, 0, DateTimeKind.Utc);
                colDisplayLabel = yr.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (colStart < queryStart)
        {
            colStart = queryStart;
        }

        if (colEnd > queryEnd)
        {
            colEnd = queryEnd;
        }

        return (colStart, colEnd, colDisplayLabel);
    }

    private (List<PivotTransactionItem> FilteredItems, string RowDisplayLabel) FilterTransactionsByRowKey(
        List<PivotTransactionItem> flatItems,
        string rowKey,
        PivotRowDimension rowDimension,
        Dictionary<Guid, Category> categoryMap,
        Dictionary<Guid, string> categoryTranslations)
    {
        string rowDisplayLabel = rowKey;
        List<PivotTransactionItem> filteredItems;

        if (rowKey.Contains("__m__", StringComparison.Ordinal))
        {
            var parts = rowKey.Split("__m__");
            var catKey = parts[0];
            var merchantName = parts[1];
            rowDisplayLabel = merchantName;

            filteredItems = flatItems.Where(t =>
            {
                var m = !string.IsNullOrWhiteSpace(t.Merchant?.Name) ? t.Merchant.Name : CleanMerchant(t.Label, t.RawLabel);
                if (!string.Equals(m, merchantName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.Equals(catKey, "uncategorized", StringComparison.Ordinal))
                {
                    return !t.CategoryId.HasValue || !categoryMap.ContainsKey(t.CategoryId.Value);
                }
                if (catKey.EndsWith("__direct", StringComparison.Ordinal))
                {
                    var pIdStr = catKey.Replace("__direct", "", StringComparison.Ordinal);
                    if (Guid.TryParse(pIdStr, out var pId))
                    {
                        return t.CategoryId == pId;
                    }
                }
                if (Guid.TryParse(catKey, out var cId))
                {
                    return t.CategoryId == cId || (t.Category != null && t.Category.ParentCategoryId == cId);
                }
                return true;
            }).ToList();
        }
        else if (string.Equals(rowKey, "uncategorized", StringComparison.Ordinal))
        {
            rowDisplayLabel = localizer["Common.Uncategorized"].Value ?? "Sans catégorie";
            filteredItems = flatItems.Where(t => !t.CategoryId.HasValue || !categoryMap.ContainsKey(t.CategoryId.Value)).ToList();
        }
        else if (rowKey.EndsWith("__direct", StringComparison.Ordinal))
        {
            var pIdStr = rowKey.Replace("__direct", "", StringComparison.Ordinal);
            if (Guid.TryParse(pIdStr, out var pId))
            {
                var pCat = categoryMap.TryGetValue(pId, out var pc) ? pc : null;
                rowDisplayLabel = pCat != null ? $"{pCat.Name} (Général)" : "Général";
                filteredItems = flatItems.Where(t => t.CategoryId == pId).ToList();
            }
            else
            {
                filteredItems = flatItems;
            }
        }
        else if (Guid.TryParse(rowKey, out var guidKey))
        {
            if (rowDimension == PivotRowDimension.Category)
            {
                var cat = categoryMap.TryGetValue(guidKey, out var c) ? c : null;
                if (cat != null)
                {
                    rowDisplayLabel = categoryTranslations.TryGetValue(cat.Id, out var tr) && !string.IsNullOrWhiteSpace(tr) ? tr : cat.Name;
                    filteredItems = flatItems.Where(t => t.CategoryId == guidKey || (t.Category != null && t.Category.ParentCategoryId == guidKey)).ToList();
                }
                else
                {
                    filteredItems = flatItems;
                }
            }
            else if (rowDimension == PivotRowDimension.Account)
            {
                filteredItems = flatItems.Where(t => t.BankAccountId == guidKey).ToList();
                var acc = filteredItems.FirstOrDefault()?.BankAccount;
                rowDisplayLabel = acc?.Name ?? "Compte";
            }
            else
            {
                filteredItems = flatItems;
            }
        }
        else if (rowDimension == PivotRowDimension.Merchant)
        {
            rowDisplayLabel = rowKey;
            filteredItems = flatItems.Where(t =>
            {
                var m = !string.IsNullOrWhiteSpace(t.Merchant?.Name) ? t.Merchant.Name : CleanMerchant(t.Label, t.RawLabel);
                return string.Equals(m, rowKey, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }
        else
        {
            filteredItems = flatItems;
        }

        return (filteredItems, rowDisplayLabel);
    }

    private static List<PivotTransactionItem> FlattenTransactions(IEnumerable<Transaction> transactions)
    {
        var items = new List<PivotTransactionItem>();
        foreach (var t in transactions)
        {
            if (t.IsSplit && t.Splits.Count > 0)
            {
                foreach (var split in t.Splits)
                {
                    items.Add(new PivotTransactionItem
                    {
                        Id = split.Id,
                        TransactionId = t.Id,
                        Date = t.Date,
                        BudgetDate = t.BudgetDate,
                        Amount = split.Amount,
                        Type = t.Type,
                        BankAccountId = t.BankAccountId,
                        BankAccount = t.BankAccount,
                        CategoryId = split.CategoryId,
                        Category = split.Category,
                        MerchantId = split.MerchantId,
                        Merchant = split.Merchant ?? t.Merchant,
                        Label = string.IsNullOrWhiteSpace(split.Notes) ? t.Label : $"{t.Label} — {split.Notes}",
                        RawLabel = t.RawLabel,
                        Notes = split.Notes ?? t.Notes,
                        CreatedAtUtc = split.CreatedAtUtc,
                        IsSplitChild = true
                    });
                }
            }
            else
            {
                items.Add(new PivotTransactionItem
                {
                    Id = t.Id,
                    TransactionId = t.Id,
                    Date = t.Date,
                    BudgetDate = t.BudgetDate,
                    Amount = t.Amount,
                    Type = t.Type,
                    BankAccountId = t.BankAccountId,
                    BankAccount = t.BankAccount,
                    CategoryId = t.CategoryId,
                    Category = t.Category,
                    MerchantId = t.MerchantId,
                    Merchant = t.Merchant,
                    Label = t.Label,
                    RawLabel = t.RawLabel,
                    Notes = t.Notes,
                    CreatedAtUtc = t.CreatedAtUtc,
                    IsSplitChild = false
                });
            }
        }
        return items;
    }

    private static bool MatchesAnyRecurring(Transaction t, List<RecurringExpense> recurringExpenses)
    {
        foreach (var rec in recurringExpenses)
        {
            if (rec.TargetBankAccountId.HasValue && rec.TargetBankAccountId.Value != t.BankAccountId)
            {
                continue;
            }

            if (RecurringExpenseService.MatchesPattern(t.Label, t.RawLabel, rec.Pattern) && rec.MatchesAmount(t.Amount))
            {
                return true;
            }
        }
        return false;
    }

    private static string CleanMerchant(string label, string? rawLabel)
    {
        var cleaned = RecurringExpenseService.CleanTransactionLabel(label, rawLabel);
        return string.IsNullOrEmpty(cleaned) ? label : cleaned;
    }

    private async Task<(DateTime Start, DateTime End)> ResolvePeriodDatesAsync(PivotPeriodPreset preset, DateTime? customStart, DateTime? customEnd)
    {
        var now = timeProvider.GetLocalNow().Date;
        DateTime start;
        DateTime end;

        switch (preset)
        {
            case PivotPeriodPreset.CurrentMonth:
                var startDay = await uiSettingsService.GetSalaryMonthStartDayAsync().ConfigureAwait(false);
                var (mStart, mEnd, _) = BudgetPeriodCalculator.GetCurrentMonthRange(now, startDay);
                start = mStart ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                end = mEnd ?? start.AddMonths(1).AddDays(-1);
                break;
            case PivotPeriodPreset.Last12Months:
                start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);
                end = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddDays(-1);
                break;
            case PivotPeriodPreset.PreviousYear:
                start = new DateTime(now.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                end = new DateTime(now.Year - 1, 12, 31, 0, 0, 0, DateTimeKind.Utc);
                break;
            case PivotPeriodPreset.Custom:
                start = customStart ?? new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                end = customEnd ?? now;
                break;
            default:
                start = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                end = new DateTime(now.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc);
                break;
        }

        if (start > end)
        {
            var temp = start;
            start = end;
            end = temp;
        }

        return (start, end);
    }
}
