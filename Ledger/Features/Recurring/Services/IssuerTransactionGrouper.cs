using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Recurring.Services;

/// <summary>
/// Regroupe les débits par émetteur nettoyé, puis scinde un émetteur qui porte plusieurs montants stables.
/// </summary>
internal static class IssuerTransactionGrouper
{
    private const int MinimumIssuerKeyLength = 3;

    public static Dictionary<string, List<Transaction>> Group(IEnumerable<Transaction> transactions)
    {
        var issuers = new Dictionary<string, List<Transaction>>(StringComparer.OrdinalIgnoreCase);

        foreach (var transaction in transactions)
        {
            var issuerKey = RecurringExpenseService.CleanTransactionLabel(transaction.Label, transaction.RawLabel);
            if (string.IsNullOrWhiteSpace(issuerKey) || issuerKey.Length < MinimumIssuerKeyLength)
            {
                continue;
            }

            if (!issuers.TryGetValue(issuerKey, out var list))
            {
                list = [];
                issuers[issuerKey] = list;
            }

            list.Add(transaction);
        }

        var groups = new Dictionary<string, List<Transaction>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, list) in issuers.Where(i => i.Value.Count >= 2))
        {
            SplitByStableAmount(groups, key, list);
        }

        return groups;
    }

    // Un même émetteur (ex. un assureur) peut prélever plusieurs contrats à montants distincts : sans scission,
    // leurs intervalles s'entremêleraient et la cadence ne serait plus détectée.
    private static void SplitByStableAmount(Dictionary<string, List<Transaction>> groups, string issuerKey, List<Transaction> list)
    {
        var stableAmounts = list
            .GroupBy(t => Math.Round(t.Amount, 1))
            .Where(g => g.Count() >= 2)
            .ToList();

        if (stableAmounts.Count <= 1)
        {
            groups[issuerKey] = list;
            return;
        }

        foreach (var amountGroup in stableAmounts)
        {
            groups[$"{issuerKey} ({amountGroup.Key:0.00} €)"] = amountGroup.OrderBy(t => t.Date).ToList();
        }
    }
}
