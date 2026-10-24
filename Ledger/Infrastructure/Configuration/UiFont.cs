namespace Ledger.Infrastructure.Configuration;

/// <summary>
/// Représente une police d'écriture disponible dans l'application, stockée en base de données.
/// </summary>
public class UiFont
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Nom convivial de la police (ex: "Inter", "Roboto", "Segoe UI").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Chaîne CSS font-family (ex: "'Inter', sans-serif").
    /// </summary>
    public required string FontFamilyCss { get; set; }

    /// <summary>
    /// URL Google Fonts ou feuille de style distante (null si police système locale).
    /// </summary>
    public string? GoogleFontUrl { get; set; }

    /// <summary>
    /// Indique si cette police est celle actuellement active pour l'interface.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Police fournie par défaut avec l'application (non supprimable).
    /// </summary>
    public bool IsSystem { get; set; } = true;

    /// <summary>
    /// Ordre d'affichage dans la liste d'administration.
    /// </summary>
    public int DisplayOrder { get; set; }
}
