using Ledger.Features.Categories.Entities;
using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Emplacement d'une transaction dans l'arbre catégorie → sous-catégorie du tableau croisé.
/// </summary>
internal readonly record struct CategoryPlacement(
    string ParentKey,
    string? ChildKey,
    string? ChildLabel,
    string? ChildIcon,
    string? ChildColor,
    bool? IsChildIncome);

/// <summary>
/// Détermine la ligne parente et la sous-ligne d'une transaction selon sa catégorie.
/// </summary>
internal sealed class CategoryPlacementResolver(
    IReadOnlyList<Category> allCategories,
    IReadOnlyDictionary<Guid, string> categoryNames,
    string generalLabel)
{
    public const string UncategorizedKey = "uncategorized";

    private readonly Dictionary<Guid, Category> _categoryMap = allCategories.ToDictionary(c => c.Id);

    private readonly HashSet<Guid> _parentIdsWithChildren = allCategories
        .Where(c => c.ParentCategoryId.HasValue)
        .Select(c => c.ParentCategoryId.GetValueOrDefault())
        .ToHashSet();

    public CategoryPlacement Resolve(PivotTransactionItem transaction)
    {
        if (!transaction.CategoryId.HasValue || !_categoryMap.TryGetValue(transaction.CategoryId.Value, out var category))
        {
            return new CategoryPlacement(UncategorizedKey, null, null, null, null, null);
        }

        if (category.ParentCategoryId.HasValue && _categoryMap.TryGetValue(category.ParentCategoryId.Value, out var parent))
        {
            return new CategoryPlacement(
                parent.Id.ToString(),
                category.Id.ToString(),
                categoryNames.TryGetValue(category.Id, out var childName) ? childName : category.Name,
                category.IconName ?? parent.IconName ?? "bi-tag",
                category.ColorHex ?? parent.ColorHex ?? "#64748b",
                category.IsIncome);
        }

        var parentKey = category.Id.ToString();
        if (!_parentIdsWithChildren.Contains(category.Id))
        {
            return new CategoryPlacement(parentKey, null, null, null, null, null);
        }

        // Une catégorie qui a des sous-catégories reçoit aussi des transactions directes ; elles sont isolées
        // dans une sous-ligne « Général » pour que la somme des sous-lignes égale celle de la ligne parente.
        return new CategoryPlacement(
            parentKey,
            $"{parentKey}__direct",
            $"{categoryNames.GetValueOrDefault(category.Id, category.Name)} ({generalLabel})",
            category.IconName ?? "bi-tag",
            category.ColorHex ?? "#64748b",
            category.IsIncome);
    }
}
