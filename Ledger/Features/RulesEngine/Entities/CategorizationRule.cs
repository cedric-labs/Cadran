using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;

namespace Ledger.Features.RulesEngine.Entities;

/// <summary>
/// Règle de catégorisation automatique basée sur une expression régulière (Regex)
/// appliquée aux libellés bruts des opérations bancaires.
/// </summary>
public class CategorizationRule : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    /// <summary>
    /// Nom descriptif de la règle (ex: "Courses Supermarché - Carrefour/Leclerc").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Motif Regex insensible à la casse appliqué sur RawLabel ou Label.
    /// Ex: "(?i)(carrefour|auchan|monoprix|lidl|e\.leclerc)"
    /// </summary>
    public required string Pattern { get; set; }

    /// <summary>
    /// Ordre d'évaluation. Plus la priorité est faible (ex: 1), plus elle est prioritaire.
    /// </summary>
    public int Priority { get; set; } = 100;

    public Guid TargetCategoryId { get; set; }
    public Category? TargetCategory { get; set; }

    /// <summary>
    /// Marchand / Tiers optionnel affecté automatiquement par la règle.
    /// </summary>
    public Guid? TargetMerchantId { get; set; }
    public Ledger.Features.Merchants.Entities.Merchant? TargetMerchant { get; set; }

    /// <summary>
    /// Filtre optionnel pour restreindre la règle à un compte précis. Si null, s'applique à tous les comptes.
    /// </summary>
    public Guid? TargetBankAccountId { get; set; }
    public BankAccount? TargetBankAccount { get; set; }

    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    /// <summary>
    /// Si vrai, décale automatiquement la date budgétaire de l'opération au 1er jour du mois suivant.
    /// Particulièrement utile pour les salaires reçus vers le 25 du mois.
    /// </summary>
    public bool ShiftToNextMonth { get; set; } = false;

    public bool IsActive { get; set; } = true;
}
