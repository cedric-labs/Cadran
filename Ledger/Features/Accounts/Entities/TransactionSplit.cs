using Ledger.Domain.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;

namespace Ledger.Features.Accounts.Entities;

/// <summary>
/// Sous-opération issue de la ventilation d'une opération parente.
/// Permet d'affecter des montants distincts à plusieurs catégories ou marchands
/// (ex: retrait DAB de 20€ scindé en 10€ Coiffeur et 10€ Alimentation).
/// </summary>
public class TransactionSplit : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    public Guid TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    /// <summary>
    /// Montant ventilé pour cette part (toujours positif, en valeur absolue).
    /// </summary>
    public decimal Amount { get; set; }

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid? MerchantId { get; set; }
    public Merchant? Merchant { get; set; }

    public string? Notes { get; set; }
}
