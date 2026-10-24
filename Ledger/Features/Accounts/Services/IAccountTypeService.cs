using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Contrat du service de gestion des types de comptes bancaires personnalisés.
/// </summary>
public interface IAccountTypeService
{
    /// <summary>
    /// Récupère la liste ordonnée de tous les types de comptes bancaires de l'utilisateur.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Liste des types de comptes personnalisés.</returns>
    Task<List<CustomAccountType>> GetAllAccountTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère un type de compte personnalisé par son identifiant unique.
    /// </summary>
    /// <param name="id">Identifiant unique du type de compte.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le type de compte correspondant, ou null s'il n'existe pas.</returns>
    Task<CustomAccountType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crée un nouveau type de compte personnalisé pour l'utilisateur.
    /// </summary>
    /// <param name="accountType">Entité du type de compte à créer.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le type de compte créé avec son identifiant généré.</returns>
    Task<CustomAccountType> CreateAsync(CustomAccountType accountType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Met à jour un type de compte personnalisé existant.
    /// </summary>
    /// <param name="accountType">Entité mise à jour.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le type de compte mis à jour.</returns>
    Task<CustomAccountType> UpdateAsync(CustomAccountType accountType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime un type de compte personnalisé s'il n'est rattaché à aucun compte actif.
    /// </summary>
    /// <param name="id">Identifiant unique du type de compte.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Succès de la suppression et message d'erreur éventuel.</returns>
    Task<(bool Success, string? ErrorMessage)> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initialise les types de comptes standards par défaut pour un nouvel utilisateur.
    /// </summary>
    /// <param name="userId">Identifiant de l'utilisateur.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task EnsureDefaultAccountTypesForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Détecte et supprime les doublons de types de comptes non utilisés, tout en préservant
    /// impérativement les types actuellement associés à un ou plusieurs comptes bancaires.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le nombre de types de comptes en doublon supprimés.</returns>
    Task<int> DeduplicateAccountTypesAsync(CancellationToken cancellationToken = default);
}
