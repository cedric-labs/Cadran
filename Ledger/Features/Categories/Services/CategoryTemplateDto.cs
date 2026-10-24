namespace Ledger.Features.Categories.Services;

/// <summary>
/// DTO représentant le gabarit d'une catégorie par défaut chargée depuis Resources/default-categories.json.
/// Supporte un dictionnaire extensible de traductions multilingues (ex: "fr", "en", "es", "de"...).
/// </summary>
public class CategoryTemplateDto
{
    public Dictionary<string, string> Translations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string ColorHex { get; set; } = "#6366f1";
    public string IconName { get; set; } = "bi-tag";
    public bool IsIncome { get; set; }
    public bool IsSalary { get; set; }
    public List<CategoryTemplateDto> SubCategories { get; set; } = new();

    /// <summary>
    /// Résout le libellé par défaut : priorité au Français ("fr"), puis Anglais ("en"), ou la 1ère traduction disponible.
    /// </summary>
    public string GetDefaultName()
    {
        if (Translations.TryGetValue("fr", out var fr) && !string.IsNullOrWhiteSpace(fr))
        {
            return fr;
        }

        if (Translations.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en))
        {
            return en;
        }

        return Translations.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "Catégorie";
    }
}
