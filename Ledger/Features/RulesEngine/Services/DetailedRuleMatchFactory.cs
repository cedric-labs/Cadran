using Ledger.Features.RulesEngine.Entities;

namespace Ledger.Features.RulesEngine.Services;

/// <summary>
/// Résume les règles reconnaissant une opération : nombre, noms dans l'ordre de priorité et cibles de la première.
/// </summary>
internal static class DetailedRuleMatchFactory
{
    /// <summary>
    /// Construit la synthèse à partir des règles correspondantes, déjà ordonnées par priorité.
    /// </summary>
    public static DetailedRuleMatch Create(Guid transactionId, IReadOnlyList<CategorizationRule> matchingRules)
    {
        var firstRule = matchingRules.FirstOrDefault();
        return new DetailedRuleMatch(
            TransactionId: transactionId,
            MatchCount: matchingRules.Count,
            FirstRuleName: firstRule?.Name,
            FirstTargetCategoryId: firstRule?.TargetCategoryId,
            FirstCategoryName: firstRule?.TargetCategory?.Name,
            MatchingRuleNames: matchingRules.Select(r => r.Name).ToList(),
            FirstTargetMerchantId: firstRule?.TargetMerchantId,
            FirstMerchantName: firstRule?.TargetMerchant?.Name);
    }
}
