using System.Globalization;
using System.Text;
using Ledger.Domain.Entities;
using Ledger.Features.Categories.Entities;

namespace Ledger.Features.Categories.Services;

/// <summary>
/// Rapproche une catégorie existante d'un gabarit par défaut, sans tenir compte des accents ni de la casse,
/// à partir de son nom et de ses traductions enregistrées.
/// </summary>
internal static class CategoryTemplateMatcher
{
    public static bool Matches(Category category, CategoryTemplateDto template, IReadOnlyList<EntityTranslation> existingTranslations)
    {
        var categoryNames = existingTranslations
            .Where(t => t.EntityId == category.Id && string.Equals(t.Field, "Name", StringComparison.OrdinalIgnoreCase))
            .Select(t => Normalize(t.Translation))
            .ToHashSet(StringComparer.Ordinal);

        categoryNames.Add(Normalize(category.Name));

        return template.Translations.Values.Any(templateTranslation =>
            categoryNames.Contains(Normalize(templateTranslation)));
    }

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return StripAccents(text.Trim()).ToLowerInvariant();
    }

    private static string StripAccents(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(text.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
