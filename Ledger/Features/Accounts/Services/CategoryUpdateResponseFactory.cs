using System.Globalization;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Construit la réponse JSON renvoyée au tableau des transactions après une mise à jour de catégorie,
/// afin qu'il rafraîchisse la ligne sans recharger la page.
/// </summary>
public static class CategoryUpdateResponseFactory
{
    public static object Create(Transaction transaction, Guid? categoryId, Category? category, Guid? merchantId, Merchant? merchant)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var budgetDate = transaction.BudgetDate;
        var isShifted = budgetDate.HasValue &&
            (budgetDate.Value.Year != transaction.Date.Year || budgetDate.Value.Month != transaction.Date.Month);

        return new
        {
            success = true,
            categoryId,
            categoryName = category?.Name ?? "Non catégorisé",
            categoryIcon = category?.IconName ?? "bi-tag",
            categoryColor = category?.ColorHex ?? "#6c757d",
            merchantId,
            merchantName = merchant?.Name ?? "",
            budgetDate = budgetDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            budgetDateStr = budgetDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            isShifted,
            budgetBadgeText = isShifted ? budgetDate?.ToString("MM/yyyy", CultureInfo.CurrentCulture) : null,
            notes = transaction.Notes,
            statusId = transaction.CustomStatusId,
            statusDisplayName = transaction.GetStatusDisplayName(),
            statusColorHex = transaction.GetStatusColorHex(),
            statusIconClass = transaction.GetStatusIconClass()
        };
    }
}
