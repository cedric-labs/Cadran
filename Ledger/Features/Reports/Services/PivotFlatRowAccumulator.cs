using Ledger.Features.Accounts.Entities;
using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Cumule les opérations dans les lignes à plat d'un tableau croisé (par compte ou par commerçant) puis retient les lignes actives.
/// </summary>
internal static class PivotFlatRowAccumulator
{
    /// <summary>
    /// Crée une ligne dont chaque colonne démarre à zéro.
    /// </summary>
    public static PivotRowDto CreateRow(IReadOnlyList<PivotColumnHeaderDto> columns, string rowKey, string label, string iconName, string colorHex, string? subLabel = null)
    {
        return new PivotRowDto
        {
            RowKey = rowKey,
            Label = label,
            SubLabel = subLabel,
            IconName = iconName,
            ColorHex = colorHex,
            CellValues = columns.ToDictionary(c => c.Key, _ => 0m, StringComparer.Ordinal),
            CellCounts = columns.ToDictionary(c => c.Key, _ => 0, StringComparer.Ordinal),
            CellHasAnomalies = columns.ToDictionary(c => c.Key, _ => false, StringComparer.Ordinal),
            CellIsCredit = columns.ToDictionary(c => c.Key, _ => false, StringComparer.Ordinal)
        };
    }

    /// <summary>
    /// Ajoute l'opération à la colonne : montant brut pour les filtres de sens unique, sinon crédit positif et débit négatif.
    /// </summary>
    public static void Add(PivotRowDto row, string columnKey, PivotTransactionItem transaction, PivotFlowFilter flowFilter)
    {
        var singleDirection = flowFilter == PivotFlowFilter.CreditsOnly || flowFilter == PivotFlowFilter.DebitsOnly;
        var delta = singleDirection || transaction.Type == TransactionType.Credit ? transaction.Amount : -transaction.Amount;

        row.CellValues[columnKey] += delta;
        row.CellCounts[columnKey] += 1;
    }

    /// <summary>
    /// Écarte les lignes sans mouvement, renseigne totaux, moyenne et sens des cellules, applique la métrique puis trie par total décroissant.
    /// </summary>
    public static List<PivotRowDto> ActiveRowsByTotal(IEnumerable<PivotRowDto> rows, IReadOnlyList<PivotColumnHeaderDto> columns, PivotQueryDto query)
    {
        int colsCount = Math.Max(1, columns.Count);
        var activeRows = new List<PivotRowDto>();

        foreach (var row in rows)
        {
            row.RowTotal = row.CellValues.Values.Sum();
            if (row.RowTotal == 0 && row.CellCounts.Values.Sum() == 0)
            {
                continue;
            }

            foreach (var columnKey in columns.Select(c => c.Key))
            {
                row.CellIsCredit[columnKey] = query.FlowFilter switch
                {
                    PivotFlowFilter.CreditsOnly => true,
                    PivotFlowFilter.DebitsOnly => false,
                    _ => row.CellValues[columnKey] >= 0
                };
            }

            row.MonthlyAverage = Math.Round(row.RowTotal / colsCount, 2);
            PivotMetricTransformer.Apply(row, query.MetricType);
            activeRows.Add(row);
        }

        return activeRows.OrderByDescending(r => r.RowTotal).ToList();
    }
}
