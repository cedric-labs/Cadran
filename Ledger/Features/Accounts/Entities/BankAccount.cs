using Ledger.Domain.Entities;

namespace Ledger.Features.Accounts.Entities;

/// <summary>
/// Compte bancaire ou support financier.
/// </summary>
public class BankAccount : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    public Guid BankId { get; set; }
    public Bank? Bank { get; set; }

    public required string Name { get; set; }
    public string? Iban { get; set; }

    /// <summary>
    /// Identifiant du compte dans les fichiers bancaires OFX (balise &lt;ACCTID&gt;).
    /// </summary>
    public string? OfxAccountId { get; set; }

    /// <summary>
    /// Type de compte énuméré (conservé pour rétrocompatibilité).
    /// </summary>
    public AccountType Type { get; set; } = AccountType.Checking;

    /// <summary>
    /// Identifiant du type de compte personnalisé configurable en base de données.
    /// </summary>
    public Guid? CustomAccountTypeId { get; set; }

    /// <summary>
    /// Type de compte configurable en base de données.
    /// </summary>
    public CustomAccountType? CustomAccountType { get; set; }

    /// <summary>
    /// Solde de départ à la création du compte.
    /// </summary>
    public decimal InitialBalance { get; set; } = 0.00m;

    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// Identifiant du profil de mapping CSV configuré par défaut pour ce compte bancaire.
    /// </summary>
    public Guid? CsvMappingProfileId { get; set; }

    public bool IsArchived { get; set; } = false;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
