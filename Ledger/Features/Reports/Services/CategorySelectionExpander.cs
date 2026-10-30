using Ledger.Features.Categories.Entities;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Étend une sélection de catégories à leurs sous-catégories directes.
/// </summary>
internal static class CategorySelectionExpander
{
    /// <summary>
    /// Retourne les identifiants sélectionnés auxquels s'ajoutent ceux des catégories dont le parent est sélectionné.
    /// </summary>
    public static HashSet<Guid> WithChildren(IReadOnlyCollection<Guid> selectedIds, IEnumerable<Category> categories)
    {
        var expanded = new HashSet<Guid>(selectedIds);
        foreach (var category in categories.Where(c => c.ParentCategoryId.HasValue && selectedIds.Contains(c.ParentCategoryId.Value)))
        {
            expanded.Add(category.Id);
        }

        return expanded;
    }
}
