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
        var categoryMap = allCategories.ToDictionary(c => c.Id);
        var uncategorizedLabel = localizer["Common.Uncategorized"].Value ?? "Sans catégorie";

        var topLevelCategories = allCategories.Where(c => !c.ParentCategoryId.HasValue).ToList();
        var rowsByParentKey = new Dictionary<string, PivotRowDto>();

        foreach (var topCat in topLevelCategories)
        {
            var key = topCat.Id.ToString();
            var label = categoryNames.TryGetValue(topCat.Id, out var n) ? n : topCat.Name;
            rowsByParentKey[key] = new PivotRowDto
            {
                RowKey = key,
                Label = label,
                IsIncome = topCat.IsIncome,
                IconName = topCat.IconName ?? "bi-tag",
                ColorHex = topCat.ColorHex ?? "#64748b",
                CellValues = columns.ToDictionary(c => c.Key, _ => 0m),
                CellCounts = columns.ToDictionary(c => c.Key, _ => 0),
                CellHasAnomalies = columns.ToDictionary(c => c.Key, _ => false),
                CellIsCredit = columns.ToDictionary(c => c.Key, _ => topCat.IsIncome)
            };
        }

        var uncatKey = "uncategorized";
        rowsByParentKey[uncatKey] = new PivotRowDto
        {
            RowKey = uncatKey,
            Label = uncategorizedLabel,
            IsIncome = false,
            IconName = "bi-question-circle",
            ColorHex = "#94a3b8",
            CellValues = columns.ToDictionary(c => c.Key, _ => 0m),
            CellCounts = columns.ToDictionary(c => c.Key, _ => 0),
            CellHasAnomalies = columns.ToDictionary(c => c.Key, _ => false),
            CellIsCredit = columns.ToDictionary(c => c.Key, _ => false)
        };

        var subRowsDict = new Dictionary<string, PivotRowDto>();
        var thirdRowsDict = new Dictionary<string, PivotRowDto>();

        var parentCatIdsWithChildren = allCategories
            .Where(c => c.ParentCategoryId.HasValue)
            .Select(c => c.ParentCategoryId.GetValueOrDefault())
            .ToHashSet();

        foreach (var t in transactions)
        {
            var colKey = GetColumnKeyForDate(t.BudgetDate ?? t.Date, query.ColumnDimension);
            if (!columns.Any(c => c.Key == colKey))
            {
                continue;
            }

            string parentKey;
            string? childKey = null;
            string? childLabel = null;
            string? childIcon = null;
            string? childColor = null;
            bool? isChildIncome = null;

            if (!t.CategoryId.HasValue || !categoryMap.TryGetValue(t.CategoryId.Value, out var cat))
            {
                parentKey = uncatKey;
            }
            else if (cat.ParentCategoryId.HasValue && categoryMap.TryGetValue(cat.ParentCategoryId.Value, out var parentCat))
            {
                parentKey = parentCat.Id.ToString();
                childKey = cat.Id.ToString();
                childLabel = categoryNames.TryGetValue(cat.Id, out var cn) ? cn : cat.Name;
                childIcon = cat.IconName ?? parentCat.IconName ?? "bi-tag";
                childColor = cat.ColorHex ?? parentCat.ColorHex ?? "#64748b";
                isChildIncome = cat.IsIncome;
            }
            else
            {
                parentKey = cat.Id.ToString();
                if (parentCatIdsWithChildren.Contains(cat.Id))
                {
                    childKey = $"{parentKey}__direct";
                    childLabel = $"{categoryNames.GetValueOrDefault(cat.Id, cat.Name)} ({localizer["Common.General"].Value ?? "Général"})";
                    childIcon = cat.IconName ?? "bi-tag";
                    childColor = cat.ColorHex ?? "#64748b";
                    isChildIncome = cat.IsIncome;
                }
            }

            if (!rowsByParentKey.TryGetValue(parentKey, out var parentRow))
            {
                continue;
            }

            bool isAnomaly = false;
            decimal delta;
            if (parentRow.IsIncome)
            {
                if (t.Type == TransactionType.Credit)
                {
                    delta = t.Amount;
                }
                else
                {
                    delta = -t.Amount;
                    isAnomaly = true;
                }
            }
            else
            {
                if (t.Type == TransactionType.Debit)
                {
                    delta = t.Amount;
                }
                else
                {
                    delta = -t.Amount;
                    isAnomaly = true;
                }
            }

            if (isAnomaly)
            {
                parentRow.CellHasAnomalies[colKey] = true;
            }

            parentRow.CellValues[colKey] += delta;
            parentRow.CellCounts[colKey] += 1;

            var merchantName = !string.IsNullOrWhiteSpace(t.Merchant?.Name)
                ? t.Merchant.Name
                : CleanMerchant(t.Label, t.RawLabel);
            if (string.IsNullOrWhiteSpace(merchantName))
            {
                merchantName = "Autre / Non identifié";
            }

            if (childKey != null)
            {
                if (!subRowsDict.TryGetValue(childKey, out var subRow))
                {
                    subRow = new PivotRowDto
                    {
                        RowKey = childKey,
                        ParentRowKey = parentKey,
                        IsSubRow = true,
                        IsIncome = isChildIncome ?? parentRow.IsIncome,
                        Label = childLabel ?? "Sous-catégorie",
                        IconName = childIcon,
                        ColorHex = childColor,
                        CellValues = columns.ToDictionary(c => c.Key, _ => 0m),
                        CellCounts = columns.ToDictionary(c => c.Key, _ => 0),
                        CellHasAnomalies = columns.ToDictionary(c => c.Key, _ => false),
                        CellIsCredit = columns.ToDictionary(c => c.Key, _ => isChildIncome ?? parentRow.IsIncome)
                    };
                    subRowsDict[childKey] = subRow;
                    parentRow.SubRows.Add(subRow);
                }

                if (isAnomaly)
                {
                    subRow.CellHasAnomalies[colKey] = true;
                }

                subRow.CellValues[colKey] += delta;
                subRow.CellCounts[colKey] += 1;

                var thirdKey = $"{childKey}__m__{merchantName}";
                if (!thirdRowsDict.TryGetValue(thirdKey, out var thirdRow))
                {
                    thirdRow = new PivotRowDto
                    {
                        RowKey = thirdKey,
                        ParentRowKey = childKey,
                        IsSubRow = true,
                        IsIncome = subRow.IsIncome,
                        Label = merchantName,
                        IconName = "bi-shop",
                        ColorHex = childColor,
                        CellValues = columns.ToDictionary(c => c.Key, _ => 0m),
                        CellCounts = columns.ToDictionary(c => c.Key, _ => 0),
                        CellHasAnomalies = columns.ToDictionary(c => c.Key, _ => false),
                        CellIsCredit = columns.ToDictionary(c => c.Key, _ => subRow.IsIncome)
                    };
                    thirdRowsDict[thirdKey] = thirdRow;
                    subRow.SubRows.Add(thirdRow);
                }

                if (isAnomaly)
                {
                    thirdRow.CellHasAnomalies[colKey] = true;
                }

                thirdRow.CellValues[colKey] += delta;
                thirdRow.CellCounts[colKey] += 1;
            }
            else
            {
                var directThirdKey = $"{parentKey}__m__{merchantName}";
                if (!thirdRowsDict.TryGetValue(directThirdKey, out var directThirdRow))
                {
                    directThirdRow = new PivotRowDto
                    {
                        RowKey = directThirdKey,
                        ParentRowKey = parentKey,
                        IsSubRow = true,
                        IsIncome = parentRow.IsIncome,
                        Label = merchantName,
                        IconName = "bi-shop",
                        ColorHex = parentRow.ColorHex,
                        CellValues = columns.ToDictionary(c => c.Key, _ => 0m),
                        CellCounts = columns.ToDictionary(c => c.Key, _ => 0),
                        CellHasAnomalies = columns.ToDictionary(c => c.Key, _ => false),
                        CellIsCredit = columns.ToDictionary(c => c.Key, _ => parentRow.IsIncome)
                    };
                    thirdRowsDict[directThirdKey] = directThirdRow;
                    parentRow.SubRows.Add(directThirdRow);
                }

                if (isAnomaly)
                {
                    directThirdRow.CellHasAnomalies[colKey] = true;
                }

                directThirdRow.CellValues[colKey] += delta;
                directThirdRow.CellCounts[colKey] += 1;
            }
        }

        int colsCount = Math.Max(1, columns.Count);
        var activeRows = new List<PivotRowDto>();

        foreach (var parentRow in rowsByParentKey.Values)
        {
            parentRow.RowTotal = parentRow.CellValues.Values.Sum();
            if (parentRow.RowTotal == 0 && parentRow.CellCounts.Values.Sum() == 0)
            {
                continue;
            }

            var colKeys = columns.Select(col => col.Key).ToList();
            foreach (var key in colKeys)
            {
                var v = parentRow.CellValues[key];
                parentRow.CellIsCredit[key] = parentRow.IsIncome ? v >= 0 : v < 0;
            }

            parentRow.MonthlyAverage = Math.Round(parentRow.RowTotal / colsCount, 2);
            ApplyMetricTransformation(parentRow, query.MetricType);

            foreach (var sub in parentRow.SubRows)
            {
                sub.RowTotal = sub.CellValues.Values.Sum();
                foreach (var key in colKeys)
                {
                    var sv = sub.CellValues[key];
                    sub.CellIsCredit[key] = sub.IsIncome ? sv >= 0 : sv < 0;
                }
                sub.MonthlyAverage = Math.Round(sub.RowTotal / colsCount, 2);
                ApplyMetricTransformation(sub, query.MetricType);

                foreach (var third in sub.SubRows)
                {
                    third.RowTotal = third.CellValues.Values.Sum();
                    foreach (var key in colKeys)
                    {
                        var tv = third.CellValues[key];
                        third.CellIsCredit[key] = third.IsIncome ? tv >= 0 : tv < 0;
                    }
                    third.MonthlyAverage = Math.Round(third.RowTotal / colsCount, 2);
                    ApplyMetricTransformation(third, query.MetricType);
                }

                sub.SubRows = sub.SubRows.OrderByDescending(m => m.RowTotal).ToList();
            }

            parentRow.SubRows = parentRow.SubRows.OrderByDescending(s => s.RowTotal).ToList();
            activeRows.Add(parentRow);
        }

        return activeRows.OrderByDescending(r => r.RowTotal).ToList();
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
                CellValues = columns.ToDictionary(c => c.Key, _ => 0m),
                CellCounts = columns.ToDictionary(c => c.Key, _ => 0),
                CellHasAnomalies = columns.ToDictionary(c => c.Key, _ => false),
                CellIsCredit = columns.ToDictionary(c => c.Key, _ => false)
            };
        }

        foreach (var t in transactions)
        {
            var colKey = GetColumnKeyForDate(t.BudgetDate ?? t.Date, query.ColumnDimension);
            if (!columns.Any(c => c.Key == colKey))
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
            if (!columns.Any(c => c.Key == colKey))
            {
                continue;
            }

            var merchantName = !string.IsNullOrWhiteSpace(t.Merchant?.Name)
                ? t.Merchant.Name
                : CleanMerchant(t.Label, t.RawLabel);
            if (string.IsNullOrWhiteSpace(merchantName))
            {
                merchantName = "Autre / Non identifié";
            }

            if (!rows.TryGetValue(merchantName, out var row))
            {
                row = new PivotRowDto
                {
                    RowKey = merchantName,
                    Label = merchantName,
                    IconName = "bi-shop",
                    ColorHex = "#64748b",
                    CellValues = columns.ToDictionary(c => c.Key, _ => 0m),
                    CellCounts = columns.ToDictionary(c => c.Key, _ => 0),
                    CellHasAnomalies = columns.ToDictionary(c => c.Key, _ => false),
                    CellIsCredit = columns.ToDictionary(c => c.Key, _ => false)
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
