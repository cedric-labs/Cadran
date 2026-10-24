using Ledger.Features.Reports.Entities;
using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Contrat du service de gestion des tableaux de bord et de calcul des métriques de reporting.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Récupère la liste de tous les tableaux de bord de l'utilisateur avec leurs widgets associés.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Liste des tableaux de bord ordonnée par date de création.</returns>
    Task<List<ReportDashboard>> GetDashboardsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère un tableau de bord spécifique par son identifiant unique.
    /// </summary>
    /// <param name="id">Identifiant unique du tableau de bord.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le tableau de bord correspondant, ou null s'il n'existe pas.</returns>
    Task<ReportDashboard?> GetDashboardByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crée un nouveau tableau de bord vide.
    /// </summary>
    /// <param name="name">Nom du tableau de bord.</param>
    /// <param name="description">Description optionnelle.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le tableau de bord créé.</returns>
    Task<ReportDashboard> CreateDashboardAsync(string name, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime un tableau de bord et tous les widgets qui lui sont rattachés.
    /// </summary>
    /// <param name="id">Identifiant unique du tableau de bord.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Vrai si la suppression a réussi.</returns>
    Task<bool> DeleteDashboardAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ajoute un widget graphique à un tableau de bord existant.
    /// </summary>
    /// <param name="dashboardId">Identifiant du tableau de bord parent.</param>
    /// <param name="title">Titre affiché du widget.</param>
    /// <param name="widgetType">Type technique du widget.</param>
    /// <param name="widthCols">Largeur en colonnes dans la grille responsive.</param>
    /// <param name="configJson">Configuration JSON personnalisée optionnelle.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>L'entité du widget créé.</returns>
    Task<ReportWidget> AddWidgetAsync(Guid dashboardId, string title, string widgetType, int widthCols, string? configJson = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Met à jour les propriétés d'un widget existant.
    /// </summary>
    /// <param name="widgetId">Identifiant unique du widget.</param>
    /// <param name="title">Nouveau titre du widget.</param>
    /// <param name="widthCols">Nouvelle largeur en colonnes.</param>
    /// <param name="configJson">Nouvelle configuration JSON optionnelle.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Le widget mis à jour, ou null s'il n'existe pas.</returns>
    Task<ReportWidget?> UpdateWidgetAsync(Guid widgetId, string title, int widthCols, string? configJson = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime un widget d'un tableau de bord.
    /// </summary>
    /// <param name="widgetId">Identifiant unique du widget à supprimer.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Vrai si le widget a été supprimé avec succès.</returns>
    Task<bool> DeleteWidgetAsync(Guid widgetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcule les séries de données et métriques pour un widget donné.
    /// </summary>
    /// <param name="widget">Entité du widget à évaluer.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>DTO contenant les métriques et données de rendu graphique.</returns>
    Task<WidgetDataDto> ComputeWidgetDataAsync(ReportWidget widget, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcule simultanément les séries de données pour l'ensemble des widgets d'un tableau de bord.
    /// </summary>
    /// <param name="dashboardId">Identifiant unique du tableau de bord.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Liste des données calculées pour chaque widget.</returns>
    Task<List<WidgetDataDto>> ComputeDashboardDataAsync(Guid dashboardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Réinitialise le tableau de bord principal avec les widgets par défaut du système.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task ResetDefaultDashboardAsync(CancellationToken cancellationToken = default);
}
