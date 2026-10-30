namespace Ledger.Features.Reports.Models;

/// <summary>
/// Type de flux financier à filtrer dans le Tableau Croisé Dynamique (TCD).
/// </summary>
public enum PivotFlowFilter
{
    All = 0,                    // Tous les flux (Dépenses et Revenus)
    DebitsOnly = 1,             // Dépenses / Débits uniquement
    CreditsOnly = 2,            // Revenus / Crédits uniquement
    RecurringOnly = 3,          // Prélèvements récurrents uniquement (charges fixes)
    NonRecurringDebitsOnly = 4  // Dépenses variables (hors charges récurrentes)
}

/// <summary>
/// Dimension d'axe en ligne pour le TCD.
/// </summary>
public enum PivotRowDimension
{
    Category = 0,               // Catégories (arborescentes avec sous-catégories)
    Merchant = 1,               // Marchands / Libellés nettoyés
    Account = 2                 // Comptes bancaires
}

/// <summary>
/// Dimension d'axe en colonne pour le TCD.
/// </summary>
public enum PivotColumnDimension
{
    Month = 0,                  // Mois (Jan, Fév, Mar...)
    Quarter = 1,                // Trimestres (T1, T2, T3, T4)
    Year = 2                    // Années
}

/// <summary>
/// Type de métrique calculée dans les cellules du TCD.
/// </summary>
public enum PivotMetricType
{
    Sum = 0,                    // Somme totale des montants
    Average = 1,                // Montant moyen par opération
    Count = 2                   // Nombre d'opérations
}

/// <summary>
/// Préréglage de période temporelle.
/// </summary>
public enum PivotPeriodPreset
{
    CurrentYear = 0,            // Année civile en cours
    Last12Months = 1,           // 12 derniers mois glissants
    PreviousYear = 2,           // Année précédente (N-1)
    CurrentMonth = 3,           // Mois en cours
    Custom = 99                 // Plage personnalisée (StartDate et EndDate)
}

/// <summary>
/// Critères de requête et filtres envoyés pour calculer un TCD.
/// </summary>
public class PivotQueryDto
{
    public PivotFlowFilter FlowFilter { get; set; } = PivotFlowFilter.DebitsOnly;
    public PivotRowDimension RowDimension { get; set; } = PivotRowDimension.Category;
    public PivotColumnDimension ColumnDimension { get; set; } = PivotColumnDimension.Month;
    public PivotMetricType MetricType { get; set; } = PivotMetricType.Sum;
    public PivotPeriodPreset PeriodPreset { get; set; } = PivotPeriodPreset.CurrentYear;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public List<Guid>? SelectedCategoryIds { get; set; }
    public List<Guid>? SelectedAccountIds { get; set; }
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Si vrai, inclut les sous-lignes détaillées pour les catégories parentes.
    /// </summary>
    public bool ExpandSubCategories { get; set; } = true;
}

/// <summary>
/// Description d'une colonne dynamique dans le TCD (ex: "2026-01", "Janvier 2026", "T1 2026").
/// </summary>
public class PivotColumnHeaderDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? SubLabel { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// Ligne de données dans la matrice du TCD (avec support de la hiérarchie parent/enfants).
/// </summary>
public class PivotRowDto
{
    public string RowKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? SubLabel { get; set; }
    public string? IconName { get; set; }
    public string? ColorHex { get; set; }

    public bool IsSubRow { get; set; } = false;
    public string? ParentRowKey { get; set; }

    // Valeur calculée par colonne (Key de la colonne -> Montant/Métrique)
    public Dictionary<string, decimal> CellValues { get; set; } = new(StringComparer.Ordinal);

    // Nombre d'opérations par colonne
    public Dictionary<string, int> CellCounts { get; set; } = new(StringComparer.Ordinal);

    // Total de la ligne
    public decimal RowTotal { get; set; }

    // Moyenne mensuelle sur la période
    public decimal MonthlyAverage { get; set; }

    // Pourcentage du total global
    public decimal PercentOfGrandTotal { get; set; }

    /// <summary>
    /// Vrai si la ligne représente des revenus/crédits, faux pour des dépenses/débits.
    /// </summary>
    public bool IsIncome { get; set; } = false;

    /// <summary>
    /// Indique pour chaque colonne si la cellule contient un flux inversé / anomalie
    /// (ex: un crédit/remboursement dans une catégorie de dépenses, ou un débit dans une catégorie de revenus).
    /// </summary>
    public Dictionary<string, bool> CellHasAnomalies { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Indique pour chaque colonne si la valeur nette de la cellule correspond à un flux net créditeur (+).
    /// </summary>
    public Dictionary<string, bool> CellIsCredit { get; set; } = new(StringComparer.Ordinal);

    // Sous-lignes éventuelles (ex: sous-catégories)
    public List<PivotRowDto> SubRows { get; set; } = new();
}

/// <summary>
/// Résultat complet calculé du Tableau Croisé Dynamique.
/// </summary>
public class PivotResultDto
{
    public PivotQueryDto Query { get; set; } = new();

    public List<PivotColumnHeaderDto> Columns { get; set; } = new();
    public List<PivotRowDto> Rows { get; set; } = new();

    // Totaux des colonnes
    public Dictionary<string, decimal> ColumnTotals { get; set; } = new(StringComparer.Ordinal);

    // Grand total consolidé
    public decimal GrandTotal { get; set; }

    // Nombre total d'opérations analysées
    public int TotalTransactionsCount { get; set; }

    // Période effective analysée
    public DateTime EffectiveStartDate { get; set; }
    public DateTime EffectiveEndDate { get; set; }
}

public class PivotCellTransactionItemDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public DateTime? BudgetDate { get; set; }
    public string BankAccountName { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? Currency { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? RawLabel { get; set; }
    public string? Notes { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryColorHex { get; set; }
    public string? CategoryIconName { get; set; }
    public Ledger.Features.Accounts.Entities.TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public bool IsCategoryIncome { get; set; }
    public bool IsAnomaly { get; set; }
}

public class PivotCellDetailDto
{
    public string RowLabel { get; set; } = string.Empty;
    public string ColumnLabel { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TransactionsCount { get; set; }
    public List<PivotCellTransactionItemDto> Transactions { get; set; } = new();
}
