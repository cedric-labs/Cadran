using Ledger.Features.Reports.Entities;
using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Contrat du service de génération des Tableaux Croisés Dynamiques (TCD) et d'analyse multidimensionnelle.
/// </summary>
public interface IPivotReportService
{
    /// <summary>
    /// Calcule la matrice du Tableau Croisé Dynamique selon les filtres et dimensions demandés.
    /// </summary>
    /// <param name="query">Paramètres et dimensions d'agrégation de la vue TCD.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Résultat matriciel du tableau croisé.</returns>
    Task<PivotResultDto> ComputePivotAsync(PivotQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère la liste des rapports TCD enregistrés par l'utilisateur connecté.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Liste des configurations TCD sauvegardées.</returns>
    Task<List<SavedPivotReport>> GetSavedPivotReportsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère un rapport TCD enregistré par son identifiant.
    /// </summary>
    /// <param name="id">Identifiant unique du rapport sauvegardé.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le rapport TCD correspondant, ou null s'il n'existe pas.</returns>
    Task<SavedPivotReport?> GetSavedPivotReportByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sauvegarde ou met à jour une vue TCD personnalisée.
    /// </summary>
    /// <param name="name">Nom de la vue sauvegardée.</param>
    /// <param name="description">Description optionnelle.</param>
    /// <param name="query">Requête et paramètres de filtrage associés.</param>
    /// <param name="existingId">Identifiant existant en cas de mise à jour.</param>
    /// <param name="isDefault">Indique si cette vue doit être la vue par défaut.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>L'entité sauvegardée.</returns>
    Task<SavedPivotReport> SavePivotReportAsync(string name, string? description, PivotQueryDto query, Guid? existingId = null, bool isDefault = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime une vue TCD sauvegardée.
    /// </summary>
    /// <param name="id">Identifiant unique du rapport à supprimer.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Vrai si le rapport a été supprimé avec succès.</returns>
    Task<bool> DeleteSavedPivotReportAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère le détail des opérations correspondant à une cellule spécifique du TCD.
    /// </summary>
    /// <param name="query">Requête de la matrice parente.</param>
    /// <param name="rowKey">Clé de ligne de la cellule.</param>
    /// <param name="colKey">Clé de colonne de la cellule.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Détail des opérations sous-jacentes de la cellule.</returns>
    Task<PivotCellDetailDto> GetCellDetailAsync(PivotQueryDto query, string rowKey, string colKey, CancellationToken cancellationToken = default);
}
