using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Convertit les montants d'une ligne de tableau croisé selon la métrique demandée : somme, nombre d'opérations ou montant moyen.
/// </summary>
internal static class PivotMetricTransformer
{
    /// <summary>
    /// Remplace les valeurs et totaux de la ligne par ceux de la métrique ; la somme les laisse inchangés.
    /// </summary>
    public static void Apply(PivotRowDto row, PivotMetricType metricType)
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
}
