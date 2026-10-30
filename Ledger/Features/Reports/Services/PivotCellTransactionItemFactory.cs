using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Construit la ligne d'opération du détail d'une cellule de tableau croisé : montant signé, catégorie localisée et indicateur d'anomalie.
/// </summary>
internal static class PivotCellTransactionItemFactory
{
    /// <summary>
    /// Une opération est en anomalie lorsque son sens contredit la nature de sa catégorie (débit d'une catégorie de revenu, crédit d'une autre).
    /// </summary>
    public static PivotCellTransactionItemDto Create(
        PivotTransactionItem transaction,
        IReadOnlyDictionary<Guid, Category> categoryMap,
        IReadOnlyDictionary<Guid, string> categoryTranslations)
    {
        string categoryName = "Sans catégorie";
        string? categoryColor = null;
        string? categoryIcon = null;
        bool isCategoryIncome = false;
        bool isAnomaly = false;

        if (transaction.CategoryId.HasValue && categoryMap.TryGetValue(transaction.CategoryId.Value, out var category))
        {
            categoryName = categoryTranslations.TryGetValue(category.Id, out var translation) && !string.IsNullOrWhiteSpace(translation) ? translation : category.Name;
            categoryColor = category.ColorHex;
            categoryIcon = category.IconName;
            isCategoryIncome = category.IsIncome;
            isAnomaly = (transaction.Type == TransactionType.Debit && category.IsIncome) || (transaction.Type == TransactionType.Credit && !category.IsIncome);
        }

        return new PivotCellTransactionItemDto
        {
            Id = transaction.Id,
            Date = transaction.Date,
            BudgetDate = transaction.BudgetDate,
            BankAccountName = transaction.BankAccount?.Name ?? "Compte",
            BankName = transaction.BankAccount?.Bank?.Name,
            Currency = transaction.BankAccount?.Currency,
            Label = transaction.Label,
            RawLabel = transaction.RawLabel,
            Notes = transaction.Notes,
            CategoryName = categoryName,
            CategoryColorHex = categoryColor,
            CategoryIconName = categoryIcon,
            Type = transaction.Type,
            Amount = transaction.Type == TransactionType.Debit ? -Math.Abs(transaction.Amount) : Math.Abs(transaction.Amount),
            IsCategoryIncome = isCategoryIncome,
            IsAnomaly = isAnomaly
        };
    }
}
