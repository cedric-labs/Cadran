using Ledger.Domain.Entities;
using Ledger.Features.Categories.Entities;

namespace Ledger.Features.Categories.Services;

/// <summary>
/// Calcule les catégories et traductions à ajouter pour qu'un utilisateur dispose de tout le catalogue par défaut,
/// en réutilisant les catégories existantes qui correspondent déjà à un gabarit.
/// </summary>
internal sealed class DefaultCategoryRestorePlan(
    string userId,
    DateTime now,
    IReadOnlyList<EntityTranslation> existingTranslations)
{
    private readonly HashSet<string> _knownTranslationKeys = existingTranslations
        .Where(t => string.Equals(t.Field, "Name", StringComparison.OrdinalIgnoreCase))
        .Select(t => TranslationKey(t.EntityId, t.LanguageCode))
        .ToHashSet(StringComparer.Ordinal);

    public List<Category> CategoriesToAdd { get; } = [];

    public List<EntityTranslation> TranslationsToAdd { get; } = [];

    /// <summary>
    /// Associe le gabarit à une catégorie existante parmi <paramref name="siblings"/>, ou en crée une,
    /// puis complète ses traductions manquantes.
    /// </summary>
    public Category Apply(CategoryTemplateDto template, List<Category> siblings, Guid? parentCategoryId)
    {
        var match = siblings.FirstOrDefault(c => CategoryTemplateMatcher.Matches(c, template, existingTranslations));
        var isNew = match is null;
        var category = match ?? CreateCategory(template, parentCategoryId);

        if (isNew)
        {
            CategoriesToAdd.Add(category);
            siblings.Add(category);
        }

        AddMissingTranslations(category, template, isNew);
        return category;
    }

    private Category CreateCategory(CategoryTemplateDto template, Guid? parentCategoryId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Name = template.GetDefaultName(),
        ColorHex = template.ColorHex,
        IconName = template.IconName,
        IsIncome = template.IsIncome,
        // Le gabarit d'une catégorie parente ne porte jamais l'indicateur de salaire : il est réservé aux sous-catégories.
        IsSalary = parentCategoryId.HasValue && template.IsSalary,
        ParentCategoryId = parentCategoryId,
        CreatedAtUtc = now
    };

    private void AddMissingTranslations(Category category, CategoryTemplateDto template, bool isNew)
    {
        foreach (var (language, text) in template.Translations)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var key = TranslationKey(category.Id, language);
            if (!isNew && _knownTranslationKeys.Contains(key))
            {
                continue;
            }

            TranslationsToAdd.Add(new EntityTranslation
            {
                EntityType = "Category",
                EntityId = category.Id,
                LanguageCode = language.ToLowerInvariant(),
                Field = "Name",
                Translation = text,
                CreatedAtUtc = now
            });
            _knownTranslationKeys.Add(key);
        }
    }

    private static string TranslationKey(Guid entityId, string languageCode) =>
        $"{entityId}:{languageCode.ToLowerInvariant()}:name";
}
