namespace Ledger.Domain.Entities;

/// <summary>
/// Stocke les traductions multilingues personnalisées ou par défaut des entités métier
/// (ex: Catégories, Types de comptes, Statuts personnalisés, etc.).
/// </summary>
public class EntityTranslation : BaseEntity
{
    /// <summary>
    /// Type de l'entité (ex: "Category", "CustomAccountType", "TransactionCustomStatus").
    /// </summary>
    public required string EntityType { get; set; }

    /// <summary>
    /// Identifiant unique de l'entité cible.
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Code langue normalisé en minuscules (ex: "fr", "en", "es").
    /// </summary>
    public required string LanguageCode { get; set; }

    /// <summary>
    /// Nom du champ traduit (ex: "Name", "Description").
    /// </summary>
    public string Field { get; set; } = "Name";

    /// <summary>
    /// Valeur textuelle traduite.
    /// </summary>
    public required string Translation { get; set; }
}
