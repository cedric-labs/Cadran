using Ledger.Infrastructure.Configuration;

namespace Ledger.Features.Settings.Services;

/// <summary>
/// Polices d'interface installées au premier démarrage, toutes locales : Inter est active, les autres sont proposées au choix.
/// </summary>
internal static class DefaultUiFontCatalog
{
    /// <summary>
    /// Crée les sept polices système dans leur ordre d'affichage.
    /// </summary>
    public static List<UiFont> Create()
    {
        return new List<UiFont>
        {
            new()
            {
                Name = "Inter",
                FontFamilyCss = "'Inter', system-ui, -apple-system, sans-serif",
                GoogleFontUrl = null,
                IsActive = true,
                IsSystem = true,
                DisplayOrder = 1
            },
            new()
            {
                Name = "Système (Défaut OS)",
                FontFamilyCss = "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
                GoogleFontUrl = null,
                IsActive = false,
                IsSystem = true,
                DisplayOrder = 2
            },
            new()
            {
                Name = "Roboto",
                FontFamilyCss = "'Roboto', system-ui, sans-serif",
                GoogleFontUrl = null,
                IsActive = false,
                IsSystem = true,
                DisplayOrder = 3
            },
            new()
            {
                Name = "Open Sans",
                FontFamilyCss = "'Open Sans', system-ui, sans-serif",
                GoogleFontUrl = null,
                IsActive = false,
                IsSystem = true,
                DisplayOrder = 4
            },
            new()
            {
                Name = "Poppins",
                FontFamilyCss = "'Poppins', system-ui, sans-serif",
                GoogleFontUrl = null,
                IsActive = false,
                IsSystem = true,
                DisplayOrder = 5
            },
            new()
            {
                Name = "Plus Jakarta Sans",
                FontFamilyCss = "'Plus Jakarta Sans', system-ui, sans-serif",
                GoogleFontUrl = null,
                IsActive = false,
                IsSystem = true,
                DisplayOrder = 6
            },
            new()
            {
                Name = "Fira Sans",
                FontFamilyCss = "'Fira Sans', system-ui, sans-serif",
                GoogleFontUrl = null,
                IsActive = false,
                IsSystem = true,
                DisplayOrder = 7
            }
        };
    }
}
