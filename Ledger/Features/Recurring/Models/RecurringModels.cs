using Ledger.Features.Accounts.Entities;
using Ledger.Features.Recurring.Entities;

namespace Ledger.Features.Recurring.Models;

/// <summary>
/// Candidat à la récurrence détecté automatiquement en analysant l'historique des opérations.
/// </summary>
public class RecurringCandidateDto
{
    public string Pattern { get; set; } = string.Empty;
    public string SuggestedName { get; set; } = string.Empty;
    public decimal AverageAmount { get; set; }
    public decimal LastAmount { get; set; }
    public int? ExpectedDayOfMonth { get; set; }
    public RecurringFrequency Frequency { get; set; } = RecurringFrequency.Monthly;
    public Guid? SuggestedCategoryId { get; set; }
    public string? SuggestedCategoryName { get; set; }
    public string? SuggestedCategoryIcon { get; set; }
    public string? SuggestedCategoryColor { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? BankAccountName { get; set; }
    public int OccurrencesCount { get; set; }
    public DateTime FirstDate { get; set; }
    public DateTime LastDate { get; set; }
    public int ConfidenceScore { get; set; } // 0 à 100%
    public bool IsAlreadyConfigured { get; set; }
    public string? ConfiguredExpenseName { get; set; }
    public List<TransactionSampleDto> SampleTransactions { get; set; } = new();
}

public class TransactionSampleDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public enum RecurringMonthStatus
{
    Pending = 1,   // En attente (la date n'est pas encore passée)
    Debited = 2,   // Déjà débité avec succès ce mois-ci
    Overdue = 3    // En retard (le jour habituel est dépassé et aucune opération correspondante trouvée)
}

/// <summary>
/// Ligne d'une charge récurrente pour un mois spécifique.
/// </summary>
public class MonthlyRecurringItemDto
{
    public RecurringExpense Expense { get; set; } = new() { Name = string.Empty, Pattern = string.Empty, UserId = string.Empty };
    public RecurringMonthStatus Status { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal? ActualDebitedAmount { get; set; }
    public DateTime? ActualDebitedDate { get; set; }
    public Guid? ActualTransactionId { get; set; }
    public string? ActualTransactionLabel { get; set; }

    /// <summary>
    /// Vrai si le montant réel diffère du montant attendu (ex: augmentation d'un abonnement).
    /// </summary>
    public bool HasPriceDifference => ActualDebitedAmount.HasValue &&
                                      Math.Abs(ActualDebitedAmount.Value - ExpectedAmount) > 0.05m;

    public decimal? PriceDifference => ActualDebitedAmount.HasValue
        ? ActualDebitedAmount.Value - ExpectedAmount
        : null;
}

/// <summary>
/// Synthèse mensuelle des charges fixes et calcul du reste à vivre.
/// </summary>
public class MonthlyRecurringSummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>
    /// Total des charges récurrentes prévues pour ce mois.
    /// </summary>
    public decimal TotalExpected { get; set; }

    /// <summary>
    /// Montant déjà prélevé à ce jour dans le mois.
    /// </summary>
    public decimal TotalDebited { get; set; }

    /// <summary>
    /// Montant restant à débiter d'ici la fin du mois.
    /// </summary>
    public decimal TotalPending { get; set; }

    /// <summary>
    /// Revenus de salaires / activité constatés ou prévus ce mois-ci.
    /// </summary>
    public decimal SalaryIncome { get; set; }

    /// <summary>
    /// Tous les revenus (salaires + autres rentrées).
    /// </summary>
    public decimal TotalIncome { get; set; }

    /// <summary>
    /// Reste à vivre prévisionnel = Revenus - Total des charges fixes prévues.
    /// </summary>
    public decimal EstimatedRemainingToLive => TotalIncome - TotalExpected;

    /// <summary>
    /// Pourcentage des charges déjà débitées (0 à 100%).
    /// </summary>
    public int ProgressPercentage => TotalExpected > 0
        ? (int)Math.Min(100, Math.Round((TotalDebited / TotalExpected) * 100))
        : 0;

    public List<MonthlyRecurringItemDto> Items { get; set; } = new();
}

public record RecurringMatchDto(
    Guid TransactionId,
    int MatchCount,
    string? FirstExpenseName,
    List<string> MatchingExpenseNames,
    Guid? FirstExpenseId = null,
    List<Guid>? MatchingExpenseIds = null);
