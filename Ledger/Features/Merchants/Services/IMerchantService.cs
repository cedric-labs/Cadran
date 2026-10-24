using Ledger.Features.Merchants.Entities;

namespace Ledger.Features.Merchants.Services;

/// <summary>
/// Service de gestion des marchands / tiers (CRUD, normalisation, auto-création, fusion).
/// </summary>
public interface IMerchantService
{
    /// <summary>
    /// Récupère la liste de tous les marchands de l'utilisateur ordonnés par nom.
    /// </summary>
    Task<List<Merchant>> GetAllMerchantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère un marchand par son identifiant.
    /// </summary>
    Task<Merchant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recherche un marchand par son nom (insensible à la casse).
    /// </summary>
    Task<Merchant?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère ou crée automatiquement un marchand par son nom.
    /// Vérifie l'unicité de manière insensible à la casse et aux espaces.
    /// </summary>
    Task<Merchant> GetOrCreateByNameAsync(string name, Guid? defaultCategoryId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crée un nouveau marchand.
    /// </summary>
    Task<Merchant> CreateAsync(string name, Guid? defaultCategoryId = null, string? iconName = null, string? colorHex = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Met à jour les informations d'un marchand.
    /// </summary>
    Task<Merchant?> UpdateAsync(Guid id, string name, Guid? defaultCategoryId = null, string? iconName = null, string? colorHex = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime un marchand et détache les opérations et règles associées.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fusionne un marchand source dans un marchand cible, puis supprime le marchand source.
    /// </summary>
    Task<int> MergeMerchantsAsync(Guid sourceMerchantId, Guid targetMerchantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retourne le décompte des opérations associées à chaque marchand.
    /// </summary>
    Task<Dictionary<Guid, int>> GetTransactionCountsByMerchantAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Normalise une chaîne de nom de marchand (trim, minuscules, suppression des espaces multiples).
    /// </summary>
    string NormalizeMerchantName(string name);
}
