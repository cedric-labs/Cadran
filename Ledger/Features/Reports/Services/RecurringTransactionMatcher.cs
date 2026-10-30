using Ledger.Features.Accounts.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Services;
using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Sépare les opérations correspondant à une charge récurrente active de celles qui n'en sont pas.
/// </summary>
internal static class RecurringTransactionMatcher
{
    /// <summary>
    /// Pour les filtres « récurrentes seules » et « débits non récurrents », retient les opérations selon leur correspondance
    /// à une charge récurrente ; tout autre filtre, ou une absence de règle, laisse la liste inchangée.
    /// </summary>
    public static List<Transaction> Apply(List<Transaction> transactions, PivotFlowFilter flowFilter, List<RecurringExpense>? rules)
    {
        if (rules == null || rules.Count == 0)
        {
            return transactions;
        }

        return flowFilter switch
        {
            PivotFlowFilter.RecurringOnly => transactions.Where(t => MatchesAny(t, rules)).ToList(),
            PivotFlowFilter.NonRecurringDebitsOnly => transactions.Where(t => !MatchesAny(t, rules)).ToList(),
            _ => transactions
        };
    }

    private static bool MatchesAny(Transaction transaction, List<RecurringExpense> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.TargetBankAccountId.HasValue && rule.TargetBankAccountId.Value != transaction.BankAccountId)
            {
                continue;
            }

            if (RecurringExpenseService.MatchesPattern(transaction.Label, transaction.RawLabel, rule.Pattern) && rule.MatchesAmount(transaction.Amount))
            {
                return true;
            }
        }

        return false;
    }
}
