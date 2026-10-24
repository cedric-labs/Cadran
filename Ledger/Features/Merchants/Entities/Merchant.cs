using Ledger.Domain.Entities;
using Ledger.Features.Categories.Entities;

namespace Ledger.Features.Merchants.Entities;

/// <summary>
/// Marchand / Tiers associé à une opération ou une règle d'affectation.
/// Permet de regrouper et nettoyer les libellés bancaires géographiques (ex: "Lidl" au lieu de "LIDL LENS", "LIDL VENDIN").
/// </summary>
public class Merchant : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    /// <summary>
    /// Nom affiché du marchand / tiers (ex: "Lidl", "TotalEnergies", "Netflix").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Nom normalisé (minuscules, sans espaces superflus) pour recherche et unicité insensible à la casse.
    /// </summary>
    public required string NormalizedName { get; set; }

    /// <summary>
    /// Catégorie par défaut optionnelle (suggérée automatiquement lors de la sélection du tiers).
    /// </summary>
    public Guid? DefaultCategoryId { get; set; }
    public Category? DefaultCategory { get; set; }

    /// <summary>
    /// Icône Bootstrap Icons (défaut: bi-shop).
    /// </summary>
    public string? IconName { get; set; } = "bi-shop";

    /// <summary>
    /// Couleur hexadécimale associée.
    /// </summary>
    public string? ColorHex { get; set; } = "#6366f1";
}
