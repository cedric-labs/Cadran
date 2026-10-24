using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.RulesEngine.Services;

/// <summary>
/// Résultat de l'évaluation unitaire d'une règle de catégorisation sur une opération bancaire.
/// </summary>
public record RuleMatchResult(bool IsMatched, Guid? CategoryId, string? RuleName, bool ShiftToNextMonth = false, Guid? TargetMerchantId = null);

/// <summary>
/// Synthèse détaillée des règles de catégorisation en correspondance avec une opération.
/// </summary>
public record DetailedRuleMatch(
    Guid TransactionId,
    int MatchCount,
    string? FirstRuleName,
    Guid? FirstTargetCategoryId,
    string? FirstCategoryName,
    List<string> MatchingRuleNames,
    Guid? FirstTargetMerchantId = null,
    string? FirstMerchantName = null);

/// <summary>
/// Contrat du moteur d'évaluation et d'application des règles de catégorisation automatique.
/// </summary>
public interface IRulesEngineService
{
    /// <summary>
    /// Évalue l'ensemble des règles actives sur une transaction individuelle.
    /// </summary>
    /// <param name="transaction">Opération bancaire à analyser.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Résultat d'évaluation indiquant si une règle correspond.</returns>
    Task<RuleMatchResult> EvaluateAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Évalue un ensemble d'opérations et produit une analyse détaillée des correspondances de règles.
    /// </summary>
    /// <param name="transactions">Collection d'opérations bancaires à tester.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Dictionnaire associant l'identifiant d'opération à ses correspondances détaillées.</returns>
    Task<Dictionary<Guid, DetailedRuleMatch>> GetMatchesForTransactionsAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applique les règles de catégorisation sur une collection d'opérations bancaires en mémoire sans écraser l'existant.
    /// </summary>
    /// <param name="transactions">Opérations à catégoriser.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Nombre d'opérations ayant été modifiées.</returns>
    Task<int> ApplyRulesToTransactionsAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applique les règles sur une collection d'opérations avec options avancées (écrasement, règle unique).
    /// </summary>
    /// <param name="transactions">Opérations à catégoriser.</param>
    /// <param name="overwriteExisting">Indique s'il faut remplacer la catégorie/marchand existant.</param>
    /// <param name="singleRuleId">Identifiant optionnel d'une règle spécifique à appliquer exclusivement.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Nombre d'opérations modifiées.</returns>
    Task<int> ApplyRulesToTransactionsAsync(IEnumerable<Transaction> transactions, bool overwriteExisting, Guid? singleRuleId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applique les règles sur l'ensemble de la base de données pour un compte ou globalement.
    /// </summary>
    /// <param name="overwriteExisting">Indique s'il faut réévaluer les opérations déjà catégorisées.</param>
    /// <param name="bankAccountId">Identifiant optionnel d'un compte bancaire cible.</param>
    /// <param name="singleRuleId">Identifiant optionnel d'une règle spécifique.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Nombre total d'opérations mises à jour en base.</returns>
    Task<int> ApplyRulesToDatabaseAsync(bool overwriteExisting = false, Guid? bankAccountId = null, Guid? singleRuleId = null, CancellationToken cancellationToken = default);
}
