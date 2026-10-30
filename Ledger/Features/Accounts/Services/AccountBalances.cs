using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Soldes d'un compte : courant (toutes les opérations), pointé (hors opérations en attente) et solde en attente (écart des deux).
/// </summary>
internal sealed record AccountBalances(decimal Current, decimal Cleared)
{
    /// <summary>Écart entre le solde courant et le solde pointé.</summary>
    public decimal Pending => Current - Cleared;

    /// <summary>
    /// Calcule les soldes à partir du solde initial et des opérations chargées avec le compte.
    /// </summary>
    public static AccountBalances Of(BankAccount account)
    {
        var current = account.InitialBalance + Net(account.Transactions);
        var cleared = account.InitialBalance + Net(account.Transactions.Where(t => t.Status != TransactionStatus.Pending));
        return new AccountBalances(current, cleared);
    }

    private static decimal Net(IEnumerable<Transaction> transactions)
    {
        var credits = transactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
        var debits = transactions.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);
        return credits - debits;
    }
}
