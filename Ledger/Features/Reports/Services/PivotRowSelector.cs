using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Reports.Models;
using Microsoft.Extensions.Localization;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Retient les opérations d'une ligne de tableau croisé à partir de sa clé (catégorie, catégorie « générale », sans catégorie,
/// compte, commerçant ou commerçant d'une catégorie) et détermine le libellé affiché de la ligne.
/// </summary>
internal sealed class PivotRowSelector(IStringLocalizer localizer)
{
    private const string MerchantSeparator = "__m__";
    private const string DirectSuffix = "__direct";
    private const string UncategorizedKey = "uncategorized";

    /// <summary>
    /// Retourne les opérations de la ligne et son libellé ; une clé non reconnue conserve toutes les opérations.
    /// </summary>
    public (List<PivotTransactionItem> FilteredItems, string RowDisplayLabel) Select(
        List<PivotTransactionItem> flatItems,
        string rowKey,
        PivotRowDimension rowDimension,
        Dictionary<Guid, Category> categoryMap,
        Dictionary<Guid, string> categoryTranslations)
    {
        if (rowKey.Contains(MerchantSeparator, StringComparison.Ordinal))
        {
            return SelectMerchantUnderCategory(flatItems, rowKey, categoryMap);
        }

        if (string.Equals(rowKey, UncategorizedKey, StringComparison.Ordinal))
        {
            return (flatItems.Where(t => IsUncategorized(t, categoryMap)).ToList(), localizer["Common.Uncategorized"].Value ?? "Sans catégorie");
        }

        if (rowKey.EndsWith(DirectSuffix, StringComparison.Ordinal))
        {
            return SelectDirectCategory(flatItems, rowKey, categoryMap);
        }

        if (Guid.TryParse(rowKey, out var guidKey))
        {
            return SelectByIdentifier(flatItems, rowKey, guidKey, rowDimension, categoryMap, categoryTranslations);
        }

        if (rowDimension == PivotRowDimension.Merchant)
        {
            return (flatItems.Where(t => string.Equals(PivotMerchantNamer.NameOf(t), rowKey, StringComparison.OrdinalIgnoreCase)).ToList(), rowKey);
        }

        return (flatItems, rowKey);
    }

    private static (List<PivotTransactionItem>, string) SelectMerchantUnderCategory(
        List<PivotTransactionItem> flatItems,
        string rowKey,
        Dictionary<Guid, Category> categoryMap)
    {
        var parts = rowKey.Split(MerchantSeparator);
        var categoryKey = parts[0];
        var merchantName = parts[1];

        var items = flatItems.Where(t =>
            string.Equals(PivotMerchantNamer.NameOf(t), merchantName, StringComparison.OrdinalIgnoreCase)
            && BelongsToCategoryKey(t, categoryKey, categoryMap)).ToList();
        return (items, merchantName);
    }

    private static bool BelongsToCategoryKey(PivotTransactionItem item, string categoryKey, Dictionary<Guid, Category> categoryMap)
    {
        if (string.Equals(categoryKey, UncategorizedKey, StringComparison.Ordinal))
        {
            return IsUncategorized(item, categoryMap);
        }

        if (categoryKey.EndsWith(DirectSuffix, StringComparison.Ordinal)
            && Guid.TryParse(categoryKey.Replace(DirectSuffix, "", StringComparison.Ordinal), out var parentId))
        {
            return item.CategoryId == parentId;
        }

        if (Guid.TryParse(categoryKey, out var categoryId))
        {
            return item.CategoryId == categoryId || (item.Category != null && item.Category.ParentCategoryId == categoryId);
        }

        return true;
    }

    private static (List<PivotTransactionItem>, string) SelectDirectCategory(
        List<PivotTransactionItem> flatItems,
        string rowKey,
        Dictionary<Guid, Category> categoryMap)
    {
        if (!Guid.TryParse(rowKey.Replace(DirectSuffix, "", StringComparison.Ordinal), out var parentId))
        {
            return (flatItems, rowKey);
        }

        var label = categoryMap.TryGetValue(parentId, out var parent) ? $"{parent.Name} (Général)" : "Général";
        return (flatItems.Where(t => t.CategoryId == parentId).ToList(), label);
    }

    private static (List<PivotTransactionItem>, string) SelectByIdentifier(
        List<PivotTransactionItem> flatItems,
        string rowKey,
        Guid id,
        PivotRowDimension rowDimension,
        Dictionary<Guid, Category> categoryMap,
        Dictionary<Guid, string> categoryTranslations)
    {
        if (rowDimension == PivotRowDimension.Category && categoryMap.TryGetValue(id, out var category))
        {
            var label = categoryTranslations.TryGetValue(category.Id, out var translation) && !string.IsNullOrWhiteSpace(translation) ? translation : category.Name;
            return (flatItems.Where(t => t.CategoryId == id || (t.Category != null && t.Category.ParentCategoryId == id)).ToList(), label);
        }

        if (rowDimension == PivotRowDimension.Account)
        {
            var items = flatItems.Where(t => t.BankAccountId == id).ToList();
            return (items, items.FirstOrDefault()?.BankAccount?.Name ?? "Compte");
        }

        return (flatItems, rowKey);
    }

    private static bool IsUncategorized(PivotTransactionItem item, Dictionary<Guid, Category> categoryMap)
    {
        return !item.CategoryId.HasValue || !categoryMap.ContainsKey(item.CategoryId.Value);
    }
}
