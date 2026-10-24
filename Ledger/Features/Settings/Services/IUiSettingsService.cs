using Ledger.Infrastructure.Configuration;

namespace Ledger.Features.Settings.Services;

/// <summary>
/// Contrat du service de gestion des préférences visuelles et paramètres d'interface utilisateur.
/// </summary>
public interface IUiSettingsService
{
    /// <summary>
    /// Récupère la police d'interface actuellement active.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>La police d'interface active.</returns>
    Task<UiFont> GetActiveFontAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère la taille de police configurée (ex. "normal", "small", "large").
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Identifiant de la taille de police.</returns>
    Task<string> GetFontSizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère le mode d'affichage par défaut (ex. "compact", "comfortable").
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Code du mode d'affichage.</returns>
    Task<string> GetDefaultDisplayModeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère la liste complète des polices d'interface installées et disponibles.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Liste de toutes les polices d'interface.</returns>
    Task<List<UiFont>> GetAllFontsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Définit la police active pour l'interface de l'application.
    /// </summary>
    /// <param name="fontId">Identifiant unique de la police sélectionnée.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task SetActiveFontAsync(Guid fontId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Définit la taille globale de police de l'interface.
    /// </summary>
    /// <param name="fontSize">Code de la taille de police.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task SetFontSizeAsync(string fontSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Définit le mode d'affichage par défaut de l'application.
    /// </summary>
    /// <param name="mode">Code du mode d'affichage.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task SetDefaultDisplayModeAsync(string mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère le preset de période temporelle par défaut (ex. "current_month").
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Code du preset de période par défaut.</returns>
    Task<string> GetDefaultPeriodAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Définit le preset de période temporelle par défaut de l'application.
    /// </summary>
    /// <param name="period">Code de la période par défaut.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task SetDefaultPeriodAsync(string period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère la taille de pagination des listes de transactions.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Nombre d'éléments affichés par page.</returns>
    Task<int> GetTransactionsPageSizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Définit la taille de pagination par défaut des listes de transactions.
    /// </summary>
    /// <param name="pageSize">Nombre d'éléments par page.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task SetTransactionsPageSizeAsync(int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indique si les centimes doivent être visibles dans les indicateurs clés (KPI).
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Vrai si les centimes sont affichés.</returns>
    Task<bool> GetShowKpiCentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Active ou désactive l'affichage des centimes dans les indicateurs clés.
    /// </summary>
    /// <param name="showCents">État d'affichage des centimes souhaité.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task SetShowKpiCentsAsync(bool showCents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ajoute une police locale personnalisée.
    /// </summary>
    /// <param name="name">Nom lisible de la police.</param>
    /// <param name="fontFamilyCss">Déclaration CSS font-family.</param>
    /// <param name="googleFontUrl">URL optionnelle (maintenue pour compatibilité historique sans CDN).</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task AddCustomFontAsync(string name, string fontFamilyCss, string? googleFontUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime une police personnalisée créée par l'utilisateur.
    /// </summary>
    /// <param name="fontId">Identifiant unique de la police à supprimer.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Succès de la suppression et message d'erreur éventuel.</returns>
    Task<(bool Success, string? ErrorMessage)> DeleteFontAsync(Guid fontId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère la configuration JSON de visibilité des colonnes pour un tableau donné.
    /// </summary>
    /// <param name="tableKey">Clé unique du tableau.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Configuration JSON des colonnes, ou null si non configuré.</returns>
    Task<string?> GetTableColumnsConfigAsync(string tableKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enregistre la configuration JSON de visibilité des colonnes pour un tableau donné.
    /// </summary>
    /// <param name="tableKey">Clé unique du tableau.</param>
    /// <param name="configJson">Configuration sérialisée en JSON.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task SetTableColumnsConfigAsync(string tableKey, string configJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère la tolérance temporelle en jours pour la détection de doublons d'opérations.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Nombre de jours de tolérance.</returns>
    Task<int> GetDuplicateToleranceDaysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Définit la tolérance temporelle en jours pour la détection de doublons.
    /// </summary>
    /// <param name="days">Nombre de jours de tolérance.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task SetDuplicateToleranceDaysAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère le jour du mois déclenchant le cycle budgétaire du salaire (1 à 28).
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Jour du mois configuré.</returns>
    Task<int> GetSalaryMonthStartDayAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Configure le jour du mois de versement du salaire pour le calcul budgétaire.
    /// </summary>
    /// <param name="day">Jour du mois (clamped entre 1 et 28).</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task SetSalaryMonthStartDayAsync(int day, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcule les bornes temporelles d'un mois budgétaire salaire selon une date de référence et le jour de début.
    /// </summary>
    /// <param name="refDate">Date de référence.</param>
    /// <param name="startDay">Jour du mois où débute le mois budgétaire.</param>
    /// <returns>Date de début et date de fin du mois budgétaire.</returns>
    (DateTime StartDate, DateTime EndDate) GetSalaryMonthBounds(DateTime refDate, int startDay);

    /// <summary>
    /// Initialise les polices et paramètres par défaut du système si la base est vide.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    Task EnsureDefaultFontsAndSettingsAsync(CancellationToken cancellationToken = default);
}
