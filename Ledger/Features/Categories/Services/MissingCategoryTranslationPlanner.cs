using Ledger.Domain.Entities;
using Ledger.Features.Categories.Entities;

namespace Ledger.Features.Categories.Services;

/// <summary>
/// Détermine les traductions de nom manquantes des catégories existantes : celles du gabarit par défaut correspondant
/// ou, à défaut de gabarit, le nom actuel enregistré comme traduction française.
/// </summary>
internal static class MissingCategoryTranslationPlanner
{
    /// <summary>
    /// Retourne les traductions à ajouter ; une langue déjà traduite (casse ignorée) n'est jamais dupliquée.
    /// </summary>
    public static List<EntityTranslation> Plan(
        IEnumerable<Category> categories,
        IReadOnlyList<EntityTranslation> existingTranslations,
        IEnumerable<CategoryTemplateDto> templates,
        DateTime nowUtc)
    {
        var existingKeys = existingTranslations
            .Select(t => $"{t.EntityId}:{t.LanguageCode.ToLowerInvariant()}:{t.Field.ToLowerInvariant()}")
            .ToHashSet(StringComparer.Ordinal);
        var flatTemplates = Flatten(templates).ToList();
        var toAdd = new List<EntityTranslation>();

        foreach (var category in categories.Where(c => !string.IsNullOrWhiteSpace(c.Name)))
        {
            var matchingTemplate = flatTemplates.FirstOrDefault(tpl => CategoryTemplateMatcher.Matches(category, tpl, existingTranslations));
            var candidates = matchingTemplate != null
                ? matchingTemplate.Translations.Select(kv => (Language: kv.Key.ToLowerInvariant(), Text: kv.Value))
                : [("fr", category.Name)];

            foreach (var (language, text) in candidates)
            {
                if (string.IsNullOrWhiteSpace(text) || !existingKeys.Add($"{category.Id}:{language}:name"))
                {
                    continue;
                }

                toAdd.Add(new EntityTranslation
                {
                    EntityType = "Category",
                    EntityId = category.Id,
                    LanguageCode = language,
                    Field = "Name",
                    Translation = text,
                    CreatedAtUtc = nowUtc
                });
            }
        }

        return toAdd;
    }

    private static IEnumerable<CategoryTemplateDto> Flatten(IEnumerable<CategoryTemplateDto> templates)
    {
        foreach (var template in templates)
        {
            yield return template;
            foreach (var sub in Flatten(template.SubCategories))
            {
                yield return sub;
            }
        }
    }
}
