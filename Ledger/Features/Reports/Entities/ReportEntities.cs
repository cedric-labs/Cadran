using Ledger.Domain.Entities;

namespace Ledger.Features.Reports.Entities;

/// <summary>
/// Tableau de bord personnalisable composé de widgets graphiques et analytiques.
/// </summary>
public class ReportDashboard : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsDefault { get; set; } = false;

    public int DisplayOrder { get; set; } = 1;

    public ICollection<ReportWidget> Widgets { get; set; } = new List<ReportWidget>();
}

/// <summary>
/// Widget analytique ou graphique (Chart.js, KPI, tableau) appartenant à un tableau de bord.
/// </summary>
public class ReportWidget : BaseEntity
{
    public Guid DashboardId { get; set; }
    public ReportDashboard? Dashboard { get; set; }

    /// <summary>
    /// Titre affiché sur la carte du widget (ex: "Répartition des dépenses", "Évolution 6 derniers mois").
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Type de widget (KpiExpenses, KpiIncome, KpiSavings, KpiRemainingToLive,
    /// ChartDoughnutExpenses, ChartDoughnutIncome, ChartBarMonthly, ChartLineSavings,
    /// TopExpensesList, RecurringGauges).
    /// </summary>
    public required string WidgetType { get; set; }

    /// <summary>
    /// Largeur du widget sur la grille Bootstrap 12 colonnes (3, 4, 6, 8 ou 12).
    /// </summary>
    public int WidthCols { get; set; } = 6;

    /// <summary>
    /// Ordre d'affichage dans la grille.
    /// </summary>
    public int DisplayOrder { get; set; } = 1;

    /// <summary>
    /// Configuration JSON personnalisée (période, filtres comptes/catégories, etc.).
    /// </summary>
    public string ConfigJson { get; set; } = "{}";
}

/// <summary>
/// Vue de Tableau Croisé Dynamique (TCD) sauvegardée par l'utilisateur.
/// </summary>
public class SavedPivotReport : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Configuration sérialisée en JSON (filtres, axes, métriques, options d'affichage).
    /// </summary>
    public required string ConfigJson { get; set; }

    public bool IsDefault { get; set; } = false;
}
