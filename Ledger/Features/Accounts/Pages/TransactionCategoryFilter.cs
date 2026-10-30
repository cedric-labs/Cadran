using Ledger.Features.Accounts.Entities;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Accounts.Pages;

/// <summary>
/// Restreint les opérations à une catégorie, sous-catégories comprises, ou aux opérations sans catégorie.
/// </summary>
internal static class TransactionCategoryFilter
{
    /// <summary>
    /// Un identifiant vide retient les opérations sans catégorie ; une catégorie parente retient aussi celles de ses sous-catégories.
    /// </summary>
    public static async Task<IQueryable<Transaction>> ApplyAsync(IQueryable<Transaction> query, AppDbContext dbContext, Guid categoryId, CancellationToken cancellationToken)
    {
        if (categoryId == Guid.Empty)
        {
            return query.Where(t => t.CategoryId == null);
        }

        var selectedCategory = await dbContext.Categories
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            .ConfigureAwait(false);

        if (selectedCategory == null || selectedCategory.SubCategories.Count == 0)
        {
            return query.Where(t => t.CategoryId == categoryId);
        }

        var allIds = selectedCategory.SubCategories.Select(s => s.Id).ToList();
        allIds.Add(selectedCategory.Id);
        return query.Where(t => t.CategoryId != null && allIds.Contains(t.CategoryId.Value));
    }
}
