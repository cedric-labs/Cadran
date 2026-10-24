using Ledger.Domain.Entities;

namespace Ledger.Features.Categories.Entities;

/// <summary>
/// Entité représentant une catégorie de transaction.
/// Inclut le nom de l'icône (ex: bi-cart, bi-house, bi-wallet2) sélectionnée visuellement
/// via le composant Icon Picker.
/// </summary>
public class Category : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Nom ou classe de l'icône choisie via l'Icon Picker (ex: "bi-cart3", "bi-fuel-pump").
    /// </summary>
    public string IconName { get; set; } = "bi-tag";

    /// <summary>
    /// Code couleur hexadécimal pour l'affichage visuel (ex: "#3b82f6").
    /// </summary>
    public string ColorHex { get; set; } = "#6366f1";

    /// <summary>
    /// Indique s'il s'agit d'une catégorie de revenus (true) ou de dépenses (false).
    /// </summary>
    public bool IsIncome { get; set; } = false;

    /// <summary>
    /// Indique s'il s'agit d'un revenu d'activité ou salaire principal (pour la vignette KPI dédiée).
    /// </summary>
    public bool IsSalary { get; set; } = false;

    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
}
