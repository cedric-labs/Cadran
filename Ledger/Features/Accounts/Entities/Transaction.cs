using Ledger.Domain.Entities;
using Ledger.Features.Categories.Entities;

namespace Ledger.Features.Accounts.Entities;

/// <summary>
/// Opération bancaire (crédit, débit, virement).
/// </summary>
public class Transaction : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    public Guid BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    public DateTime Date { get; set; }
    public DateTime? ValueDate { get; set; }

    /// <summary>
    /// Date budgétaire d'imputation (optionnelle). Si renseignée, les filtres de périodes
    /// et les calculs budgétaires se basent sur cette date au lieu de Date.
    /// Utile pour reporter un salaire versé en fin de mois sur le budget du mois suivant.
    /// </summary>
    public DateTime? BudgetDate { get; set; }

    /// <summary>
    /// Montant en valeur absolue ou signé. Par convention,
    /// Type=Debit => Amount positif stocké, mais débité du solde.
    /// </summary>
    public decimal Amount { get; set; }

    public TransactionType Type { get; set; } = TransactionType.Debit;
    public TransactionStatus Status { get; set; } = TransactionStatus.Cleared;

    /// <summary>
    /// Statut personnalisable lié à la table des statuts.
    /// </summary>
    public Guid? CustomStatusId { get; set; }
    public TransactionCustomStatus? CustomStatus { get; set; }

    /// <summary>
    /// Nom affiché du statut (utilise le statut personnalisé s'il existe, sinon l'énumération par défaut).
    /// </summary>
    public string GetStatusDisplayName() => CustomStatus?.Name ?? Status.ToDisplayName();

    /// <summary>
    /// Couleur hexadécimale du statut.
    /// </summary>
    public string GetStatusColorHex() => CustomStatus?.ColorHex ?? (Status switch
    {
        TransactionStatus.Reconciled => "#198754",
        TransactionStatus.Cleared => "#0dcaf0",
        TransactionStatus.Pending => "#ffc107",
        _ => "#6c757d"
    });

    /// <summary>
    /// Classe de l'icône du statut.
    /// </summary>
    public string GetStatusIconClass() => CustomStatus?.IconName ?? Status.ToIconClass();

    /// <summary>
    /// Libellé nettoyé ou personnalisé par l'utilisateur.
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// Libellé brut d'origine transmis par le fichier bancaire (utilisé par le moteur de règles).
    /// </summary>
    public string? RawLabel { get; set; }

    /// <summary>
    /// Référence ou identifiant unique d'opération bancaire (ex: FITID en OFX, référence virement).
    /// </summary>
    public string? Reference { get; set; }

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>
    /// Marchand / Tiers optionnel associé à l'opération.
    /// </summary>
    public Guid? MerchantId { get; set; }
    public Ledger.Features.Merchants.Entities.Merchant? Merchant { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Identifiant de lot d'import le cas échéant.
    /// </summary>
    public Guid? ImportBatchId { get; set; }

    /// <summary>
    /// Indique si cette opération a été scindée / ventilée en plusieurs sous-opérations.
    /// </summary>
    public bool IsSplit { get; set; } = false;

    /// <summary>
    /// Sous-opérations issues de la ventilation de cette transaction.
    /// </summary>
    public ICollection<TransactionSplit> Splits { get; set; } = new List<TransactionSplit>();
}
