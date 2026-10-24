using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;

namespace Ledger.Features.Recurring.Entities;

public enum RecurringFrequency
{
    Monthly = 1,     // Mensuel (ex: loyer, netflix, box internet)
    Quarterly = 2,   // Trimestriel
    Biannual = 3,    // Semestriel
    Annual = 4       // Annuel (ex: assurance, taxe foncière, cotisation)
}

/// <summary>
/// Représente une charge ou un prélèvement récurrent (abonnement, loyer, crédit, etc.).
/// Permet d'anticiper le budget mensuel incompressible et de calculer le reste à vivre dès le 1er du mois.
/// </summary>
public class RecurringExpense : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    /// <summary>
    /// Nom usuel ou descriptif (ex: "Loyer Appartement", "Abonnement Netflix", "Assurance Habitation").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Motif texte ou mot-clé pour rapprocher automatiquement les transactions bancaires réelles.
    /// Ex: "NETFLIX", "EDF", "FREE TELECOM", "PRLV SEPA CREDIT".
    /// </summary>
    public required string Pattern { get; set; }

    /// <summary>
    /// Montant habituel attendu (positif, en euros).
    /// </summary>
    public decimal ExpectedAmount { get; set; }

    /// <summary>
    /// Tolérance en pourcentage (ex: 0.10 pour ±10% pour factures fluctuantes comme l'électricité).
    /// 0 pour un abonnement à montant strictement fixe.
    /// </summary>
    public decimal AmountTolerancePercentage { get; set; } = 0.05m;

    /// <summary>
    /// Fréquence de prélèvement (mensuel par défaut).
    /// </summary>
    public RecurringFrequency Frequency { get; set; } = RecurringFrequency.Monthly;

    /// <summary>
    /// Jour habituel du mois où intervient le prélèvement (1 à 31).
    /// </summary>
    public int? ExpectedDayOfMonth { get; set; }

    /// <summary>
    /// Catégorie cible assignée.
    /// </summary>
    public Guid? TargetCategoryId { get; set; }
    public Category? TargetCategory { get; set; }

    /// <summary>
    /// Compte bancaire prélevé (optionnel, null = n'importe quel compte).
    /// </summary>
    public Guid? TargetBankAccountId { get; set; }
    public BankAccount? TargetBankAccount { get; set; }

    /// <summary>
    /// Indique si la charge est active ou résiliée/suspendue.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indique si cette charge a été initialement détectée par l'algorithme d'analyse d'historique.
    /// </summary>
    public bool AutoDetected { get; set; } = false;

    /// <summary>
    /// Date de la dernière transaction bancaire ayant fait correspondre cette charge.
    /// </summary>
    public DateTime? LastDetectedDate { get; set; }

    /// <summary>
    /// Dernier montant réel prélevé lors de la dernière transaction trouvée.
    /// </summary>
    public decimal? LastDetectedAmount { get; set; }

    /// <summary>
    /// Notes ou informations complémentaires (ex: "Engagement jusqu'en déc. 2026", "Résiliation prévue").
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Vérifie si un montant de transaction correspond au montant attendu selon la tolérance configurée.
    /// Si ExpectedAmount est 0 ou négatif, n'importe quel montant est accepté.
    /// </summary>
    public bool MatchesAmount(decimal transactionAmount) =>
        MatchesAmount(ExpectedAmount, AmountTolerancePercentage, transactionAmount);

    /// <summary>
    /// Vérifie si un montant correspond à un montant attendu selon un pourcentage de tolérance.
    /// </summary>
    public static bool MatchesAmount(decimal expectedAmount, decimal tolerancePercentage, decimal transactionAmount)
    {
        if (expectedAmount <= 0) return true;

        var absAmount = Math.Abs(transactionAmount);
        var tol = Math.Max(0m, tolerancePercentage);
        var toleranceAmount = expectedAmount * tol;
        var min = Math.Max(0m, expectedAmount - toleranceAmount - 0.01m);
        var max = expectedAmount + toleranceAmount + 0.01m;

        return absAmount >= min && absAmount <= max;
    }
}
