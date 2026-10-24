using Ledger.Domain.Entities;

namespace Ledger.Features.Accounts.Entities;

/// <summary>
/// Établissement bancaire rattaché à un utilisateur.
/// </summary>
public class Bank : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public string? Bic { get; set; }
    public string? LogoUrl { get; set; }
    public string ColorHex { get; set; } = "#2563eb";

    public ICollection<BankAccount> Accounts { get; set; } = new List<BankAccount>();
}
