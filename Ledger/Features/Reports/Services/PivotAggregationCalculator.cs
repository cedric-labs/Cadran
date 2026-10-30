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
        var cols = new List<PivotColumnHeaderDto>();
        var culture = CultureInfo.CurrentUICulture;

        if (colDimension == PivotColumnDimension.Month)
        {
            var cur = new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endMonth = new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            int order = 1;

            while (cur <= endMonth)
            {
                var key = cur.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                var monthName = cur.ToString("MMM yyyy", culture);
                monthName = char.ToUpper(monthName[0], culture) + monthName[1..];

                cols.Add(new PivotColumnHeaderDto
                {
                    Key = key,
                    Label = monthName,
                    SortOrder = order++
                });

                cur = cur.AddMonths(1);
            }
        }
        else if (colDimension == PivotColumnDimension.Quarter)
        {
            var curYear = start.Year;
            var endYear = end.Year;
            int order = 1;

            for (int y = curYear; y <= endYear; y++)
            {
                for (int q = 1; q <= 4; q++)
                {
                    int qStartMonth = (q - 1) * 3 + 1;
                    var qStart = new DateTime(y, qStartMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                    var qEnd = qStart.AddMonths(3).AddDays(-1);

                    if (qEnd >= start && qStart <= end)
                    {
                        var key = $"{y}-Q{q}";
                        cols.Add(new PivotColumnHeaderDto
                        {
                            Key = key,
                            Label = $"T{q} {y}",
                            SortOrder = order++
                        });
                    }
                }
            }
        }
        else // Year
        {
            int order = 1;
            for (int y = start.Year; y <= end.Year; y++)
            {
                cols.Add(new PivotColumnHeaderDto
                {
                    Key = y.ToString(CultureInfo.InvariantCulture),
                    Label = y.ToString(CultureInfo.InvariantCulture),
                    SortOrder = order++
                });
            }
        }

        return cols;
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
        ApplyMetricTransformation(row, metricType);
    }

    private static string ResolveMerchantName(PivotTransactionItem transaction)
    {
        var merchantName = !string.IsNullOrWhiteSpace(transaction.Merchant?.Name)
            ? transaction.Merchant.Name
            : CleanMerchant(transaction.Label, transaction.RawLabel);
        return string.IsNullOrWhiteSpace(merchantName) ? "Autre / Non identifié" : merchantName;
    }

    private List<PivotRowDto> AggregateByAccount(
        IReadOnlyList<PivotTransactionItem> transactions,
        IReadOnlyList<PivotColumnHeaderDto> columns,
        IReadOnlyList<BankAccount> allAccounts,
        PivotQueryDto query)
    {
        var rows = new Dictionary<Guid, PivotRowDto>();

        foreach (var acc in allAccounts)
        {
            rows[acc.Id] = new PivotRowDto
            {
                RowKey = acc.Id.ToString(),
                Label = acc.Name,
                SubLabel = acc.Bank?.Name,
                IconName = acc.CustomAccountType?.IconName ?? "bi-wallet2",
                ColorHex = acc.CustomAccountType?.ColorHex ?? "#2563eb",
                CellValues = columns.ToDictionary(c => c.Key, _ => 0m, StringComparer.Ordinal),
                CellCounts = columns.ToDictionary(c => c.Key, _ => 0, StringComparer.Ordinal),
                CellHasAnomalies = columns.ToDictionary(c => c.Key, _ => false, StringComparer.Ordinal),
                CellIsCredit = columns.ToDictionary(c => c.Key, _ => false, StringComparer.Ordinal)
            };
        }

        foreach (var t in transactions)
        {
            var colKey = GetColumnKeyForDate(t.BudgetDate ?? t.Date, query.ColumnDimension);
            if (!columns.Any(c => string.Equals(c.Key, colKey, StringComparison.Ordinal)))
            {
                continue;
            }

            if (rows.TryGetValue(t.BankAccountId, out var row))
            {
                decimal delta;
                if (query.FlowFilter == PivotFlowFilter.CreditsOnly || query.FlowFilter == PivotFlowFilter.DebitsOnly)
                {
                    delta = t.Amount;
                }
                else
                {
                    delta = t.Type == TransactionType.Credit ? t.Amount : -t.Amount;
                }

                row.CellValues[colKey] += delta;
                row.CellCounts[colKey] += 1;
            }
        }

        int colsCount = Math.Max(1, columns.Count);
        var activeRows = new List<PivotRowDto>();
        var colKeys = columns.Select(c => c.Key).ToList();

        foreach (var row in rows.Values)
        {
            row.RowTotal = row.CellValues.Values.Sum();
            if (row.RowTotal == 0 && row.CellCounts.Values.Sum() == 0)
            {
                continue;
            }

            foreach (var colKey in colKeys)
            {
                if (query.FlowFilter == PivotFlowFilter.CreditsOnly)
                {
                    row.CellIsCredit[colKey] = true;
                }
                else if (query.FlowFilter == PivotFlowFilter.DebitsOnly)
                {
                    row.CellIsCredit[colKey] = false;
                }
                else
                {
                    row.CellIsCredit[colKey] = row.CellValues[colKey] >= 0;
                }
            }

            row.MonthlyAverage = Math.Round(row.RowTotal / colsCount, 2);
            ApplyMetricTransformation(row, query.MetricType);
            activeRows.Add(row);
        }

        return activeRows.OrderByDescending(r => r.RowTotal).ToList();
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
                row = new PivotRowDto
                {
                    RowKey = merchantName,
                    Label = merchantName,
                    IconName = "bi-shop",
                    ColorHex = "#64748b",
                    CellValues = columns.ToDictionary(c => c.Key, _ => 0m, StringComparer.Ordinal),
                    CellCounts = columns.ToDictionary(c => c.Key, _ => 0, StringComparer.Ordinal),
                    CellHasAnomalies = columns.ToDictionary(c => c.Key, _ => false, StringComparer.Ordinal),
                    CellIsCredit = columns.ToDictionary(c => c.Key, _ => false, StringComparer.Ordinal)
                };
                rows[merchantName] = row;
            }

            decimal delta;
            if (query.FlowFilter == PivotFlowFilter.CreditsOnly || query.FlowFilter == PivotFlowFilter.DebitsOnly)
            {
                delta = t.Amount;
            }
            else
            {
                delta = t.Type == TransactionType.Credit ? t.Amount : -t.Amount;
            }

            row.CellValues[colKey] += delta;
            row.CellCounts[colKey] += 1;
        }

        int colsCount = Math.Max(1, columns.Count);
        var activeRows = new List<PivotRowDto>();
        var colKeys = columns.Select(c => c.Key).ToList();

        foreach (var row in rows.Values)
        {
            row.RowTotal = row.CellValues.Values.Sum();
            if (row.RowTotal == 0 && row.CellCounts.Values.Sum() == 0)
            {
                continue;
            }

            foreach (var colKey in colKeys)
            {
                if (query.FlowFilter == PivotFlowFilter.CreditsOnly)
                {
                    row.CellIsCredit[colKey] = true;
                }
                else if (query.FlowFilter == PivotFlowFilter.DebitsOnly)
                {
                    row.CellIsCredit[colKey] = false;
                }
                else
                {
                    row.CellIsCredit[colKey] = row.CellValues[colKey] >= 0;
                }
            }

            row.MonthlyAverage = Math.Round(row.RowTotal / colsCount, 2);
            ApplyMetricTransformation(row, query.MetricType);
            activeRows.Add(row);
        }

        return activeRows.OrderByDescending(r => r.RowTotal).ToList();
    }

    private static void ApplyMetricTransformation(PivotRowDto row, PivotMetricType metricType)
    {
        if (metricType == PivotMetricType.Count)
        {
            foreach (var k in row.CellValues.Keys.ToList())
            {
                row.CellValues[k] = row.CellCounts[k];
            }
            row.RowTotal = row.CellCounts.Values.Sum();
            row.MonthlyAverage = Math.Round(row.RowTotal / Math.Max(1, row.CellValues.Count), 1);
        }
        else if (metricType == PivotMetricType.Average)
        {
            foreach (var k in row.CellValues.Keys.ToList())
            {
                var count = row.CellCounts[k];
                row.CellValues[k] = count > 0 ? Math.Round(row.CellValues[k] / count, 2) : 0m;
            }
            var totalCount = row.CellCounts.Values.Sum();
            var totalSum = row.RowTotal;
            row.RowTotal = totalCount > 0 ? Math.Round(totalSum / totalCount, 2) : 0m;
            row.MonthlyAverage = row.RowTotal;
        }
    }

    private static string CleanMerchant(string label, string? rawLabel)
    {
        var cleaned = RecurringExpenseService.CleanTransactionLabel(label, rawLabel);
        return string.IsNullOrEmpty(cleaned) ? label : cleaned;
    }
}
