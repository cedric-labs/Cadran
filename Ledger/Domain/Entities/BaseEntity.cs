namespace Ledger.Domain.Entities;

/// <summary>
/// Classe de base pour les entités du domaine portant un identifiant unique et des timestamps d'audit.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
