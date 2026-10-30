using Ledger.Features.Accounts.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Models;

namespace Ledger.Features.Recurring.Services;

/// <summary>
/// Compose la synthèse d'un mois : revenus, puis état de chaque charge récurrente due ce mois-ci (débitée, en attente ou en retard).
/// </summary>
internal static class MonthlyRecurringSummaryBuilder
{
    // Au-delà de ce nombre de jours après la date attendue, une charge non débitée du mois courant est en retard.
    private const int OverdueGraceDays = 3;

    /// <summary>
    /// Rapproche chaque charge active des débits du mois : une opération ne sert qu'une fois, la plus proche du montant attendu l'emporte.
    /// </summary>
    public static MonthlyRecurringSummaryDto Build(
        int year,
        int month,
        DateTime today,
        IReadOnlyList<RecurringExpense> activeExpenses,
        IReadOnlyList<Transaction> monthTransactions)
    {
        var summary = new MonthlyRecurringSummaryDto { Year = year, Month = month };

        var creditTransactions = monthTransactions.Where(t => t.Type == TransactionType.Credit).ToList();
        summary.TotalIncome = creditTransactions.Sum(t => t.Amount);
        summary.SalaryIncome = creditTransactions
            .Where(t => t.Category != null && t.Category.IsSalary)
            .Sum(t => t.Amount);

        var usedTransactionIds = new HashSet<Guid>();
        var startOfMonth = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

        foreach (var expense in activeExpenses.Where(e => RecurringExpenseService.AppliesToMonth(e.Frequency, month)))
        {
            var item = new MonthlyRecurringItemDto { Expense = expense, ExpectedAmount = expense.ExpectedAmount };
            var match = FindClosestDebit(expense, monthTransactions, usedTransactionIds);

            if (match != null)
            {
                usedTransactionIds.Add(match.Id);
                item.Status = RecurringMonthStatus.Debited;
                item.ActualDebitedAmount = match.Amount;
                item.ActualDebitedDate = match.BudgetDate ?? match.Date;
                item.ActualTransactionId = match.Id;
                item.ActualTransactionLabel = match.Label;
                summary.TotalDebited += match.Amount;
            }
            else
            {
                item.Status = ResolveUnpaidStatus(expense, startOfMonth, today);
                summary.TotalPending += expense.ExpectedAmount;
            }

            summary.TotalExpected += expense.ExpectedAmount;
            summary.Items.Add(item);
        }

        return summary;
    }

    private static Transaction? FindClosestDebit(RecurringExpense expense, IReadOnlyList<Transaction> monthTransactions, HashSet<Guid> usedTransactionIds)
    {
        return monthTransactions
            .Where(t => t.Type == TransactionType.Debit && !usedTransactionIds.Contains(t.Id))
            .Where(t => expense.TargetBankAccountId == null || t.BankAccountId == expense.TargetBankAccountId.Value)
            .Where(t => RecurringExpenseService.MatchesPattern(t.Label, t.RawLabel, expense.Pattern))
            .Where(t => expense.MatchesAmount(t.Amount))
            .OrderBy(t => Math.Abs(t.Amount - expense.ExpectedAmount))
            .FirstOrDefault();
    }

    private static RecurringMonthStatus ResolveUnpaidStatus(RecurringExpense expense, DateTime startOfMonth, DateTime today)
    {
        var isCurrentMonth = startOfMonth.Year == today.Year && startOfMonth.Month == today.Month;
        var isPastMonth = startOfMonth < new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (isPastMonth)
        {
            return RecurringMonthStatus.Overdue;
        }

        var isLate = isCurrentMonth && expense.ExpectedDayOfMonth.HasValue && today.Day > expense.ExpectedDayOfMonth.Value + OverdueGraceDays;
        return isLate ? RecurringMonthStatus.Overdue : RecurringMonthStatus.Pending;
    }
}
