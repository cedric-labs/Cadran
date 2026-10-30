using Ledger.Features.Accounts.Entities;
using Ledger.Features.RulesEngine.Entities;

namespace Ledger.Features.RulesEngine.Services;

/// <summary>
/// Reporte sur une opération les effets d'une règle qui la reconnaît : catégorie, commerçant et décalage de la date budgétaire.
/// </summary>
internal static class RuleEffectApplier
{
    /// <summary>
    /// Applique la règle et retourne vrai si au moins un champ a changé. Le commerçant n'est remplacé que s'il est absent ou si l'écrasement est demandé.
    /// </summary>
    public static bool Apply(Transaction transaction, CategorizationRule rule, bool overwriteExisting)
    {
        bool changed = false;

        if (transaction.CategoryId != rule.TargetCategoryId)
        {
            transaction.CategoryId = rule.TargetCategoryId;
            changed = true;
        }

        if (rule.TargetMerchantId.HasValue && (overwriteExisting || !transaction.MerchantId.HasValue) && transaction.MerchantId != rule.TargetMerchantId.Value)
        {
            transaction.MerchantId = rule.TargetMerchantId.Value;
            changed = true;
        }

        if (rule.ShiftToNextMonth)
        {
            var targetBudgetDate = new DateTime(transaction.Date.Year, transaction.Date.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
            if (transaction.BudgetDate != targetBudgetDate)
            {
                transaction.BudgetDate = targetBudgetDate;
                changed = true;
            }
        }

        return changed;
    }
}
