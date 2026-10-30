using System.Globalization;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Renseigne les indicateurs chiffrés (dépenses, revenus, épargne) d'un widget à partir des opérations de la période.
/// </summary>
internal static class KpiWidgetFiller
{
    private const string ExpenseColor = "#ef4444";
    private const string IncomeColor = "#10b981";

    /// <summary>Total des débits de la période.</summary>
    public static void FillExpenses(WidgetDataDto result, IReadOnlyCollection<Transaction> transactions, string periodLabel)
    {
        var total = SumOf(transactions, TransactionType.Debit);
        result.KpiValue = total;
        result.KpiFormattedValue = total.ToString("C2", CultureInfo.CurrentCulture);
        result.KpiSubtext = periodLabel;
        result.KpiIcon = "bi-arrow-down-circle";
        result.KpiColor = ExpenseColor;
    }

    /// <summary>Total des crédits de la période.</summary>
    public static void FillIncome(WidgetDataDto result, IReadOnlyCollection<Transaction> transactions, string periodLabel)
    {
        var total = SumOf(transactions, TransactionType.Credit);
        result.KpiValue = total;
        result.KpiFormattedValue = total.ToString("C2", CultureInfo.CurrentCulture);
        result.KpiSubtext = periodLabel;
        result.KpiIcon = "bi-arrow-up-circle";
        result.KpiColor = IncomeColor;
    }

    /// <summary>Crédits moins débits de la période, signé « + » lorsque le solde est positif ou nul.</summary>
    public static void FillSavings(WidgetDataDto result, IReadOnlyCollection<Transaction> transactions, string periodLabel)
    {
        var net = SumOf(transactions, TransactionType.Credit) - SumOf(transactions, TransactionType.Debit);
        result.KpiValue = net;
        result.KpiFormattedValue = (net >= 0 ? "+" : "") + net.ToString("C2", CultureInfo.CurrentCulture);
        result.KpiSubtext = periodLabel;
        result.KpiIcon = "bi-piggy-bank";
        result.KpiColor = net >= 0 ? IncomeColor : ExpenseColor;
    }

    private static decimal SumOf(IEnumerable<Transaction> transactions, TransactionType type)
    {
        return transactions.Where(t => t.Type == type).Sum(t => t.Amount);
    }
}
