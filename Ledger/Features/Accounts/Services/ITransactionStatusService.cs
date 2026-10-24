using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Contrat du service de gestion des statuts personnalisés d'opérations bancaires.
/// </summary>
public interface ITransactionStatusService
{
    /// <summary>
    /// Récupère la liste de tous les statuts d'opérations pour l'utilisateur connecté.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Liste ordonnée des statuts personnalisés.</returns>
    Task<List<TransactionCustomStatus>> GetAllStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère le statut appliqué par défaut lors de la création d'opérations.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le statut par défaut, ou null si aucun n'est configuré.</returns>
    Task<TransactionCustomStatus?> GetDefaultStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère un statut par son identifiant unique.
    /// </summary>
    /// <param name="id">Identifiant unique du statut.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le statut trouvé, ou null s'il n'existe pas.</returns>
    Task<TransactionCustomStatus?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crée un nouveau statut d'opération pour l'utilisateur.
    /// </summary>
    /// <param name="status">Entité du statut à créer.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le statut créé avec son identifiant assigné.</returns>
    Task<TransactionCustomStatus> CreateAsync(TransactionCustomStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Met à jour les propriétés d'un statut existant.
    /// </summary>
    /// <param name="status">Entité du statut contenant les nouvelles valeurs.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le statut mis à jour.</returns>
    Task<TransactionCustomStatus> UpdateAsync(TransactionCustomStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime un statut personnalisé s'il n'est utilisé par aucune opération.
    /// </summary>
    /// <param name="id">Identifiant unique du statut.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Succès de la suppression et message d'erreur le cas échéant.</returns>
    Task<(bool Success, string? ErrorMessage)> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compte le nombre d'opérations associées à un statut donné.
    /// </summary>
    /// <param name="statusId">Identifiant unique du statut.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Nombre d'opérations affectées à ce statut.</returns>
    Task<int> GetTransactionCountForStatusAsync(Guid statusId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initialise les statuts natifs par défaut pour un nouvel utilisateur.
    /// </summary>
    /// <param name="userId">Identifiant de l'utilisateur.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task EnsureDefaultStatusesForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Détecte et supprime les doublons de statuts d'opérations non utilisés, tout en préservant
    /// impérativement les statuts actuellement associés à une ou plusieurs transactions.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le nombre de statuts en doublon supprimés.</returns>
    Task<int> DeduplicateStatusesAsync(CancellationToken cancellationToken = default);
}
