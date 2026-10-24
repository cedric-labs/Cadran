namespace Ledger.Domain.Entities;

/// <summary>
/// Contrat pour toutes les entités partitionnées par utilisateur.
/// Exploité par le filtre global de requêtes EF Core (Global Query Filter)
/// et l'assignation automatique lors de l'enregistrement.
/// </summary>
public interface IUserOwnedEntity
{
    /// <summary>
    /// Identifiant de l'utilisateur propriétaire de l'entité.
    /// </summary>
    string UserId { get; set; }
}
