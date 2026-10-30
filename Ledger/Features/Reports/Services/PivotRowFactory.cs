using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Crée les lignes du tableau croisé avec une cellule initialisée à zéro pour chaque colonne.
/// </summary>
internal static class PivotRowFactory
{
    public static PivotRowDto Create(
        IReadOnlyList<PivotColumnHeaderDto> columns,
        string rowKey,
        string label,
        bool isIncome,
        string? iconName,
        string? colorHex,
        string? parentRowKey = null,
        string? subLabel = null)
    {
        return new PivotRowDto
        {
            RowKey = rowKey,
            Label = label,
            SubLabel = subLabel,
            IsIncome = isIncome,
            IconName = iconName,
            ColorHex = colorHex,
            ParentRowKey = parentRowKey,
            IsSubRow = parentRowKey is not null,
            CellValues = columns.ToDictionary(c => c.Key, _ => 0m, StringComparer.Ordinal),
            CellCounts = columns.ToDictionary(c => c.Key, _ => 0, StringComparer.Ordinal),
            CellHasAnomalies = columns.ToDictionary(c => c.Key, _ => false, StringComparer.Ordinal),
            CellIsCredit = columns.ToDictionary(c => c.Key, _ => isIncome, StringComparer.Ordinal)
        };
    }

    public static void Accumulate(PivotRowDto row, string columnKey, decimal delta, bool isAnomaly)
    {
        if (isAnomaly)
        {
            row.CellHasAnomalies[columnKey] = true;
        }

        row.CellValues[columnKey] += delta;
        row.CellCounts[columnKey] += 1;
    }
}
