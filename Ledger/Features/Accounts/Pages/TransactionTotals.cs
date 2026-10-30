using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Pages;

/// <summary>
/// Totaux affichés au-dessus d'une liste d'opérations filtrée : nombre, débits, crédits dont salaires et autres crédits.
/// </summary>
internal sealed record TransactionTotals(int Count, decimal Debits, decimal Credits, decimal SalaryCredits)
{
    /// <summary>Crédits hors catégories de salaire.</summary>
    public decimal OtherCredits => Credits - SalaryCredits;

    /// <summary>
    /// Calcule les totaux d'une liste d'opérations dont la catégorie est chargée.
    /// </summary>
    public static TransactionTotals Of(IReadOnlyCollection<Transaction> transactions)
    {
        return new TransactionTotals(
            transactions.Count,
            transactions.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount),
            transactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount),
            transactions.Where(t => t.Type == TransactionType.Credit && t.Category != null && t.Category.IsSalary).Sum(t => t.Amount));
    }
}
