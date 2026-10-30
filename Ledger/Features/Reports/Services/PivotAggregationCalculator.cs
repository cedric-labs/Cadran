using System.Globalization;
using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Recurring.Services;
using Ledger.Features.Reports.Models;
using Microsoft.Extensions.Localization;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Calculateur spécialisé dans l'agrégation, la structuration hiérarchique et la transformation métrique des rapports pivots.
/// </summary>
public sealed class PivotAggregationCalculator(IStringLocalizer localizer) : IPivotAggregationCalculator
{
    public List<PivotColumnHeaderDto> BuildColumnHeaders(DateTime start, DateTime end, PivotColumnDimension colDimension)
    {
        return colDimension switch
        {
            PivotColumnDimension.Month => PivotColumnHeaderFactory.Months(start, end),
            PivotColumnDimension.Quarter => PivotColumnHeaderFactory.Quarters(start, end),
            _ => PivotColumnHeaderFactory.Years(start, end)
        };
    }

    public string GetColumnKeyForDate(DateTime date, PivotColumnDimension dim)
    {
        return dim switch
        {
            PivotColumnDimension.Quarter => $"{date.Year}-Q{((date.Month - 1) / 3) + 1}",
            PivotColumnDimension.Year => date.Year.ToString(CultureInfo.InvariantCulture),
            _ => date.ToString("yyyy-MM", CultureInfo.InvariantCulture)
        };
    }

    public List<PivotRowDto> AggregateRows(
        IReadOnlyList<PivotTransactionItem> transactions,
        IReadOnlyList<PivotColumnHeaderDto> columns,
        IReadOnlyList<Category> allCategories,
        IReadOnlyList<BankAccount> allAccounts,
        IReadOnlyDictionary<Guid, string> categoryNames,
        PivotQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(query);

        return query.RowDimension switch
        {
            PivotRowDimension.Category => AggregateByCategory(transactions, columns, allCategories, categoryNames, query),
            PivotRowDimension.Account => AggregateByAccount(transactions, columns, allAccounts, query),
            _ => AggregateByMerchant(transactions, columns, query)
        };
    }

    public void CalculateTotalsAndPercentages(PivotResultDto result)
    {
        ArgumentNullException.ThrowIfNull(result);

        foreach (var col in result.Columns)
        {
            result.ColumnTotals[col.Key] = 0m;
        }

        decimal grandTotal = 0m;
        var colKeys = result.Columns.Select(c => c.Key).ToList();
        foreach (var row in result.Rows)
        {
            foreach (var colKey in colKeys)
            {
                if (row.CellValues.TryGetValue(colKey, out var val))
                {
                    result.ColumnTotals[colKey] += val;
                }
            }
            grandTotal += row.RowTotal;
        }

        result.GrandTotal = grandTotal;

        if (grandTotal > 0)
        {
            foreach (var row in result.Rows)
            {
                row.PercentOfGrandTotal = Math.Round((row.RowTotal / grandTotal) * 100m, 1);
                foreach (var sub in row.SubRows)
                {
                    sub.PercentOfGrandTotal = Math.Round((sub.RowTotal / grandTotal) * 100m, 1);
                    foreach (var third in sub.SubRows)
                    {
                        third.PercentOfGrandTotal = Math.Round((third.RowTotal / grandTotal) * 100m, 1);
                    }
                }
            }
        }
    }

    private List<PivotRowDto> AggregateByCategory(
        IReadOnlyList<PivotTransactionItem> transactions,
        IReadOnlyList<PivotColumnHeaderDto> columns,
        IReadOnlyList<Category> allCategories,
        IReadOnlyDictionary<Guid, string> categoryNames,
        PivotQueryDto query)
    {
        var topRows = CreateTopLevelCategoryRows(columns, allCategories, categoryNames);
        var resolver = new CategoryPlacementResolver(allCategories, categoryNames, localizer["Common.General"].Value ?? "Général");
        var tree = new CategoryPivotTree(columns);

        foreach (var t in transactions)
        {
            var colKey = GetColumnKeyForDate(t.BudgetDate ?? t.Date, query.ColumnDimension);
            if (!columns.Any(c => string.Equals(c.Key, colKey, StringComparison.Ordinal)))
            {
                continue;
            }

            var placement = resolver.Resolve(t);
            if (!topRows.TryGetValue(placement.ParentKey, out var parentRow))
            {
                continue;
            }

            // Une opération de sens contraire à la catégorie (remboursement sur une dépense) vient en déduction et signale une anomalie.
            var expectedType = parentRow.IsIncome ? TransactionType.Credit : TransactionType.Debit;
            var isAnomaly = t.Type != expectedType;
            var delta = isAnomaly ? -t.Amount : t.Amount;

            PivotRowFactory.Accumulate(parentRow, colKey, delta, isAnomaly);
            tree.Add(parentRow, placement, ResolveMerchantName(t), colKey, delta, isAnomaly);
        }

        return FinalizeCategoryRows(topRows.Values, columns, query.MetricType);
    }

    private Dictionary<string, PivotRowDto> CreateTopLevelCategoryRows(
        IReadOnlyList<PivotColumnHeaderDto> columns,
        IReadOnlyList<Category> allCategories,
        IReadOnlyDictionary<Guid, string> categoryNames)
    {
        var rows = new Dictionary<string, PivotRowDto>(StringComparer.Ordinal);

        foreach (var topCat in allCategories.Where(c => !c.ParentCategoryId.HasValue))
        {
            var key = topCat.Id.ToString();
            var label = categoryNames.TryGetValue(topCat.Id, out var name) ? name : topCat.Name;
            rows[key] = PivotRowFactory.Create(columns, key, label, topCat.IsIncome, topCat.IconName ?? "bi-tag", topCat.ColorHex ?? "#64748b");
        }

        var uncategorizedLabel = localizer["Common.Uncategorized"].Value ?? "Sans catégorie";
        rows[CategoryPlacementResolver.UncategorizedKey] = PivotRowFactory.Create(
            columns, CategoryPlacementResolver.UncategorizedKey, uncategorizedLabel, false, "bi-question-circle", "#94a3b8");
        return rows;
    }

    private static List<PivotRowDto> FinalizeCategoryRows(
        IEnumerable<PivotRowDto> topRows,
        IReadOnlyList<PivotColumnHeaderDto> columns,
        PivotMetricType metricType)
    {
        var colKeys = columns.Select(col => col.Key).ToList();
        int colsCount = Math.Max(1, columns.Count);
        var activeRows = new List<PivotRowDto>();

        foreach (var parentRow in topRows)
        {
            if (parentRow.CellValues.Values.Sum() == 0 && parentRow.CellCounts.Values.Sum() == 0)
            {
                continue;
            }

            FinalizeCategoryRow(parentRow, colKeys, colsCount, metricType);
            foreach (var sub in parentRow.SubRows)
            {
                FinalizeCategoryRow(sub, colKeys, colsCount, metricType);
                foreach (var merchant in sub.SubRows)
                {
                    FinalizeCategoryRow(merchant, colKeys, colsCount, metricType);
                }

                sub.SubRows = sub.SubRows.OrderByDescending(m => m.RowTotal).ToList();
            }

            parentRow.SubRows = parentRow.SubRows.OrderByDescending(s => s.RowTotal).ToList();
            activeRows.Add(parentRow);
        }

        return activeRows.OrderByDescending(r => r.RowTotal).ToList();
    }

    private static void FinalizeCategoryRow(PivotRowDto row, List<string> colKeys, int colsCount, PivotMetricType metricType)
    {
        row.RowTotal = row.CellValues.Values.Sum();
        foreach (var key in colKeys)
        {
            var value = row.CellValues[key];
            row.CellIsCredit[key] = row.IsIncome ? value >= 0 : value < 0;
        }

        row.MonthlyAverage = Math.Round(row.RowTotal / colsCount, 2);
        PivotMetricTransformer.Apply(row, metricType);
    }

    private static string ResolveMerchantName(PivotTransactionItem transaction)
    {
        var merchantName = PivotMerchantNamer.NameOf(transaction);
        return string.IsNullOrWhiteSpace(merchantName) ? "Autre / Non identifié" : merchantName;
    }

    private List<PivotRowDto> AggregateByAccount(
        IReadOnlyList<PivotTransactionItem> transactions,
        IReadOnlyList<PivotColumnHeaderDto> columns,
        IReadOnlyList<BankAccount> allAccounts,
        PivotQueryDto query)
    {
        var rows = allAccounts.ToDictionary(
            acc => acc.Id,
            acc => PivotFlatRowAccumulator.CreateRow(
                columns,
                acc.Id.ToString(),
                acc.Name,
                acc.CustomAccountType?.IconName ?? "bi-wallet2",
                acc.CustomAccountType?.ColorHex ?? "#2563eb",
                acc.Bank?.Name));

        foreach (var t in transactions)
        {
            var colKey = GetColumnKeyForDate(t.BudgetDate ?? t.Date, query.ColumnDimension);
            if (columns.Any(c => string.Equals(c.Key, colKey, StringComparison.Ordinal)) && rows.TryGetValue(t.BankAccountId, out var row))
            {
                PivotFlatRowAccumulator.Add(row, colKey, t, query.FlowFilter);
            }
        }

        return PivotFlatRowAccumulator.ActiveRowsByTotal(rows.Values, columns, query);
    }

    private List<PivotRowDto> AggregateByMerchant(
        IReadOnlyList<PivotTransactionItem> transactions,
        IReadOnlyList<PivotColumnHeaderDto> columns,
        PivotQueryDto query)
    {
        var rows = new Dictionary<string, PivotRowDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in transactions)
        {
            var colKey = GetColumnKeyForDate(t.BudgetDate ?? t.Date, query.ColumnDimension);
            if (!columns.Any(c => string.Equals(c.Key, colKey, StringComparison.Ordinal)))
            {
                continue;
            }

            var merchantName = ResolveMerchantName(t);
            if (!rows.TryGetValue(merchantName, out var row))
            {
                row = PivotFlatRowAccumulator.CreateRow(columns, merchantName, merchantName, "bi-shop", "#64748b");
                rows[merchantName] = row;
            }

            PivotFlatRowAccumulator.Add(row, colKey, t, query.FlowFilter);
        }

        return PivotFlatRowAccumulator.ActiveRowsByTotal(rows.Values, columns, query);
    }
}
