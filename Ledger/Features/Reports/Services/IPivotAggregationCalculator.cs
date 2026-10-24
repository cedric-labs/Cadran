using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Contrat du calculateur d'agrégations et de métriques pour les rapports tableaux croisés dynamiques.
/// </summary>
public interface IPivotAggregationCalculator
{
    /// <summary>
    /// Construit les en-têtes de colonnes selon la dimension temporelle sélectionnée.
    /// </summary>
    /// <param name="start">Date de début effective.</param>
    /// <param name="end">Date de fin effective.</param>
    /// <param name="colDimension">Dimension de colonne (Mois, Trimestre, Année).</param>
    /// <returns>Liste ordonnée des en-têtes de colonnes.</returns>
    List<PivotColumnHeaderDto> BuildColumnHeaders(DateTime start, DateTime end, PivotColumnDimension colDimension);

    /// <summary>
    /// Agrémente et structure les lignes du tableau croisé selon la dimension demandée (Catégorie, Compte, Commerçant).
    /// </summary>
    /// <param name="transactions">Transactions aplaties.</param>
    /// <param name="columns">En-têtes de colonnes calculés.</param>
    /// <param name="allCategories">Référentiel des catégories.</param>
    /// <param name="allAccounts">Référentiel des comptes bancaires.</param>
    /// <param name="categoryNames">Libellés traduits des catégories.</param>
    /// <param name="query">Requête de rapport.</param>
    /// <returns>Lignes agrégées et hiérarchisées.</returns>
    List<PivotRowDto> AggregateRows(
        IReadOnlyList<PivotTransactionItem> transactions,
        IReadOnlyList<PivotColumnHeaderDto> columns,
        IReadOnlyList<Category> allCategories,
        IReadOnlyList<BankAccount> allAccounts,
        IReadOnlyDictionary<Guid, string> categoryNames,
        PivotQueryDto query);

    /// <summary>
    /// Calcule les totaux de colonnes, le grand total et les pourcentages par ligne.
    /// </summary>
    /// <param name="result">Résultat du rapport à compléter.</param>
    void CalculateTotalsAndPercentages(PivotResultDto result);

    /// <summary>
    /// Résout la clé de colonne pivot pour une date et une dimension temporelle données.
    /// </summary>
    /// <param name="date">Date de l'opération.</param>
    /// <param name="dim">Dimension temporelle.</param>
    /// <returns>Clé textuelle unique de colonne (ex. 2026-03, 2026-Q1, 2026).</returns>
    string GetColumnKeyForDate(DateTime date, PivotColumnDimension dim);
}
