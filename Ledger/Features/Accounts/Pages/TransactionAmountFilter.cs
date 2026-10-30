using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Pages;

/// <summary>
/// Restreint les opérations par comparaison de montant : supérieur, inférieur, égal ou compris entre deux bornes.
/// </summary>
internal static class TransactionAmountFilter
{
    /// <summary>
    /// Applique l'opérateur (« gt », « gte », « lt », « lte », « eq » ou « between ») ; un opérateur inconnu laisse la requête inchangée.
    /// Pour « between », la borne haute absente vaut la valeur et les bornes sont réordonnées.
    /// </summary>
    public static IQueryable<Transaction> Apply(IQueryable<Transaction> query, string amountOperator, decimal value, decimal? maxValue)
    {
        switch (amountOperator.ToLowerInvariant())
        {
            case "gt":
                return query.Where(t => t.Amount > value);
            case "gte":
                return query.Where(t => t.Amount >= value);
            case "lt":
                return query.Where(t => t.Amount < value);
            case "lte":
                return query.Where(t => t.Amount <= value);
            case "eq":
                return query.Where(t => t.Amount == value);
            case "between":
                var upper = maxValue ?? value;
                var minBound = Math.Min(value, upper);
                var maxBound = Math.Max(value, upper);
                return query.Where(t => t.Amount >= minBound && t.Amount <= maxBound);
            default:
                return query;
        }
    }
}
