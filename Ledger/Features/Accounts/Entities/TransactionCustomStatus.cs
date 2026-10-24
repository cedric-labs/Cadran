using Ledger.Domain.Entities;

namespace Ledger.Features.Accounts.Entities;

/// <summary>
/// Statut personnalisable pour une transaction bancaire (ex: Pointée, Comptabilisée, En attente, À vérifier, En litige, etc.).
/// </summary>
public class TransactionCustomStatus : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    /// <summary>
    /// Nom affiché du statut (ex: "Pointée", "Comptabilisée", "En attente", "À vérifier").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Code système optionnel pour interopérabilité ("Reconciled", "Cleared", "Pending").
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Description explicative pour l'utilisateur.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Couleur hexadécimale du badge (ex: "#198754", "#0dcaf0", "#ffc107", "#6f42c1", "#dc3545").
    /// </summary>
    public string ColorHex { get; set; } = "#3b82f6";

    /// <summary>
    /// Classe de l'icône Bootstrap (ex: "bi-check2-all", "bi-check-lg", "bi-hourglass-split", "bi-question-circle").
    /// </summary>
    public string IconName { get; set; } = "bi-check-circle";

    /// <summary>
    /// Statut système natif (les 3 statuts de base ne peuvent pas être supprimés).
    /// </summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>
    /// Statut appliqué par défaut lors de l'import ou création d'une opération.
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Ordre d'affichage dans les listes déroulantes.
    /// </summary>
    public int DisplayOrder { get; set; } = 0;
}
