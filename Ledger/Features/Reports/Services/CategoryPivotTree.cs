using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Construit à la demande les sous-lignes (sous-catégorie) et les lignes feuilles (commerçant) sous une ligne de catégorie.
/// </summary>
internal sealed class CategoryPivotTree(IReadOnlyList<PivotColumnHeaderDto> columns)
{
    private readonly Dictionary<string, PivotRowDto> _subRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PivotRowDto> _merchantRows = new(StringComparer.Ordinal);

    public void Add(
        PivotRowDto parentRow,
        CategoryPlacement placement,
        string merchantName,
        string columnKey,
        decimal delta,
        bool isAnomaly)
    {
        PivotRowDto merchantParent = parentRow;
        var merchantColor = parentRow.ColorHex;

        if (placement.ChildKey is not null)
        {
            merchantParent = GetOrAddSubRow(parentRow, placement);
            merchantColor = placement.ChildColor;
            PivotRowFactory.Accumulate(merchantParent, columnKey, delta, isAnomaly);
        }

        var merchantRow = GetOrAddMerchantRow(merchantParent, merchantName, merchantColor);
        PivotRowFactory.Accumulate(merchantRow, columnKey, delta, isAnomaly);
    }

    private PivotRowDto GetOrAddSubRow(PivotRowDto parentRow, CategoryPlacement placement)
    {
        var childKey = placement.ChildKey ?? throw new InvalidOperationException("La sous-ligne exige une clé de sous-catégorie.");
        if (_subRows.TryGetValue(childKey, out var existing))
        {
            return existing;
        }

        var subRow = PivotRowFactory.Create(
            columns,
            childKey,
            placement.ChildLabel ?? "Sous-catégorie",
            placement.IsChildIncome ?? parentRow.IsIncome,
            placement.ChildIcon,
            placement.ChildColor,
            parentRow.RowKey);
        _subRows[childKey] = subRow;
        parentRow.SubRows.Add(subRow);
        return subRow;
    }

    private PivotRowDto GetOrAddMerchantRow(PivotRowDto owner, string merchantName, string? colorHex)
    {
        var key = $"{owner.RowKey}__m__{merchantName}";
        if (_merchantRows.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var merchantRow = PivotRowFactory.Create(columns, key, merchantName, owner.IsIncome, "bi-shop", colorHex, owner.RowKey);
        _merchantRows[key] = merchantRow;
        owner.SubRows.Add(merchantRow);
        return merchantRow;
    }
}
