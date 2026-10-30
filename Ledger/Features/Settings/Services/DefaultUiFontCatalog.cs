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
        return
        [
            SystemFont("Inter", "'Inter', system-ui, -apple-system, sans-serif", displayOrder: 1, isActive: true),
            SystemFont("Système (Défaut OS)", "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif", displayOrder: 2),
            SystemFont("Roboto", "'Roboto', system-ui, sans-serif", displayOrder: 3),
            SystemFont("Open Sans", "'Open Sans', system-ui, sans-serif", displayOrder: 4),
            SystemFont("Poppins", "'Poppins', system-ui, sans-serif", displayOrder: 5),
            SystemFont("Plus Jakarta Sans", "'Plus Jakarta Sans', system-ui, sans-serif", displayOrder: 6),
            SystemFont("Fira Sans", "'Fira Sans', system-ui, sans-serif", displayOrder: 7)
        ];
    }

    private static UiFont SystemFont(string name, string fontFamilyCss, int displayOrder, bool isActive = false)
    {
        return new UiFont
        {
            Name = name,
            FontFamilyCss = fontFamilyCss,
            GoogleFontUrl = null,
            IsActive = isActive,
            IsSystem = true,
            DisplayOrder = displayOrder
        };
    }
}
