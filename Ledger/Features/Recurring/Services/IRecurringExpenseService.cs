using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Models;

namespace Ledger.Features.Recurring.Services;

/// <summary>
/// Contrat du service de détection, calcul et suivi des charges et revenus récurrents.
/// </summary>
public interface IRecurringExpenseService
{
    /// <summary>
    /// Récupère la liste de toutes les charges récurrentes configurées.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Liste des charges récurrentes ordonnées.</returns>
    Task<List<RecurringExpense>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère une charge récurrente par son identifiant unique.
    /// </summary>
    /// <param name="id">Identifiant unique de la charge récurrente.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>La charge récurrente correspondante, ou null.</returns>
    Task<RecurringExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crée une nouvelle charge récurrente.
    /// </summary>
    /// <param name="expense">Entité de la charge récurrente à enregistrer.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>La charge récurrente créée.</returns>
    Task<RecurringExpense> CreateAsync(RecurringExpense expense, CancellationToken cancellationToken = default);

    /// <summary>
    /// Met à jour une charge récurrente existante.
    /// </summary>
    /// <param name="expense">Entité mise à jour.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>La charge récurrente modifiée, ou null si non trouvée.</returns>
    Task<RecurringExpense?> UpdateAsync(RecurringExpense expense, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime une charge récurrente.
    /// </summary>
    /// <param name="id">Identifiant unique de la charge à supprimer.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Vrai si la suppression a réussi.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcule la synthèse budgétaire mensuelle des charges et revenus récurrents pour un mois donné.
    /// </summary>
    /// <param name="year">Année civile concernée.</param>
    /// <param name="month">Mois civil concerné (1-12).</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Synthèse mensuelle avec statut de débit de chaque charge.</returns>
    Task<MonthlyRecurringSummaryDto> GetMonthlySummaryAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyse l'historique des opérations pour identifier de nouvelles charges récurrentes potentielles.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Liste des charges candidates détectées par régularité.</returns>
    Task<List<RecurringCandidateDto>> DetectCandidatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Valide une charge candidate et la convertit en charge récurrente active.
    /// </summary>
    /// <param name="candidate">Candidat récurrent à transformer.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>La charge récurrente nouvellement créée.</returns>
    Task<RecurringExpense> ConfirmCandidateAsync(RecurringCandidateDto candidate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ajoute un motif textuel à la liste des suggestions récurrentes ignorées.
    /// </summary>
    /// <param name="pattern">Motif textuel à ignorer.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task IgnoreCandidateAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère l'ensemble des motifs actuellement ignorés par l'utilisateur.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Liste des motifs ignorés.</returns>
    Task<List<string>> GetIgnoredPatternsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retire un motif de la liste d'exclusion des charges candidates.
    /// </summary>
    /// <param name="pattern">Motif textuel à réhabiliter.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task UnignoreCandidateAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Évalue une collection d'opérations et associe à chacune la charge récurrente correspondante le cas échéant.
    /// </summary>
    /// <param name="transactions">Opérations à tester.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Dictionnaire associant l'identifiant d'opération à son statut de correspondance récurrente.</returns>
    Task<Dictionary<Guid, RecurringMatchDto>> GetMatchesForTransactionsAsync(IEnumerable<Ledger.Features.Accounts.Entities.Transaction> transactions, CancellationToken cancellationToken = default);
}
