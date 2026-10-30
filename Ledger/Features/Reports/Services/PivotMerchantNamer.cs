using Ledger.Features.Recurring.Services;
using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Détermine le nom de commerçant d'une opération : celui du commerçant rattaché, à défaut le libellé nettoyé.
/// </summary>
internal static class PivotMerchantNamer
{
    /// <summary>
    /// Retourne le nom du commerçant rattaché ou, à défaut, le libellé débarrassé de ses éléments techniques.
    /// </summary>
    public static string NameOf(PivotTransactionItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Merchant?.Name))
        {
            return item.Merchant.Name;
        }

        var cleaned = RecurringExpenseService.CleanTransactionLabel(item.Label, item.RawLabel);
        return string.IsNullOrEmpty(cleaned) ? item.Label : cleaned;
    }
}
