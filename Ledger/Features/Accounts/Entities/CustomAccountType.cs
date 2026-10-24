using Ledger.Domain.Entities;

namespace Ledger.Features.Accounts.Entities;

/// <summary>
/// Catégorie macro d'un type de compte pour les calculs de patrimoine et KPIs.
/// </summary>
public enum AccountCategoryGroup
{
    /// <summary>
    /// Liquidités courantes (compte de dépôt, espèces, cartes débit).
    /// </summary>
    Liquid = 0,

    /// <summary>
    /// Épargne liquide et à terme (Livret A, LDDS, PEL, etc.).
    /// </summary>
    Savings = 1,

    /// <summary>
    /// Investissements et placements financiers (PEA, CTO, Parts sociales, etc.).
    /// </summary>
    Investment = 2,

    /// <summary>
    /// Passifs / Dettes / Cartes de crédit spécifiques.
    /// </summary>
    Debt = 3
}

/// <summary>
/// Type de compte bancaire configurable par l'utilisateur et stocké en base de données.
/// </summary>
public class CustomAccountType : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    /// <summary>
    /// Nom affiché du type de compte (ex: "Compte de Dépôt", "Livret A", "Carré mauve (PEL)", "Compte Parts Sociales").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Code technique optionnel ou identifiant standard ("Checking", "Savings", "LivretA", "Pel"...).
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Description optionnelle ou informations pour l'utilisateur.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Groupe macro de patrimoine pour les calculs et statistiques.
    /// </summary>
    public AccountCategoryGroup CategoryGroup { get; set; } = AccountCategoryGroup.Savings;

    /// <summary>
    /// Couleur hexadécimale du badge (ex: "#3b82f6", "#0dcaf0", "#198754", "#ffc107").
    /// </summary>
    public string ColorHex { get; set; } = "#0dcaf0";

    /// <summary>
    /// Icône Bootstrap affichée (ex: "bi-wallet2", "bi-piggy-bank", "bi-graph-up", "bi-credit-card").
    /// </summary>
    public string IconName { get; set; } = "bi-piggy-bank";

    /// <summary>
    /// Indique si c'est un type de compte proposé par défaut par le système.
    /// </summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>
    /// Ordre d'affichage dans les sélecteurs.
    /// </summary>
    public int DisplayOrder { get; set; } = 0;
}
