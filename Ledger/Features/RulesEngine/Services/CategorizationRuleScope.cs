using Ledger.Features.Accounts.Entities;
using Ledger.Features.RulesEngine.Entities;

namespace Ledger.Features.RulesEngine.Services;

/// <summary>
/// Vérifie qu'une opération entre dans le périmètre d'une règle : compte ciblé et bornes de montant optionnels.
/// </summary>
internal static class CategorizationRuleScope
{
    /// <summary>
    /// Retourne vrai lorsque le compte correspond (ou n'est pas restreint) et que le montant respecte les bornes, inclusives.
    /// </summary>
    public static bool Admits(CategorizationRule rule, Transaction transaction)
    {
        return (!rule.TargetBankAccountId.HasValue || rule.TargetBankAccountId.Value == transaction.BankAccountId)
            && (!rule.MinAmount.HasValue || transaction.Amount >= rule.MinAmount.Value)
            && (!rule.MaxAmount.HasValue || transaction.Amount <= rule.MaxAmount.Value);
    }
}
