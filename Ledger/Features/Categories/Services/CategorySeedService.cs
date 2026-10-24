using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Ledger.Domain.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ledger.Features.Categories.Services;

/// <summary>
/// Service assurant l'initialisation, la synchronisation et la restauration des catégories par défaut
/// avec leurs traductions multilingues à partir de Resources/default-categories.json.
/// </summary>
public class CategorySeedService(AppDbContext dbContext, ILogger<CategorySeedService> logger) : ICategorySeedService
{
    private static IReadOnlyList<CategoryTemplateDto>? _cachedCatalog;
    private static readonly Lock CatalogLock = new();
    private static readonly JsonSerializerOptions DefaultJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly AppDbContext _dbContext = dbContext;
    private readonly ILogger<CategorySeedService> _logger = logger;

    /// <summary>
    /// Récupère l'ensemble des modèles de catégories par défaut depuis le fichier de ressources JSON multilingue.
    /// Les modèles sont mis en cache statique après le premier chargement.
    /// </summary>
    public static IReadOnlyList<CategoryTemplateDto> GetDefaultCategoryTemplates()
    {
        if (_cachedCatalog != null)
        {
            return _cachedCatalog;
        }

        lock (CatalogLock)
        {
            if (_cachedCatalog != null)
            {
                return _cachedCatalog;
            }

            var assembly = typeof(CategorySeedService).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("default-categories.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var json = reader.ReadToEnd();
                    _cachedCatalog = JsonSerializer.Deserialize<List<CategoryTemplateDto>>(json, DefaultJsonOptions)
                        ?? new List<CategoryTemplateDto>();
                    return _cachedCatalog;
                }
            }

            // Fallback système de fichiers si la ressource incorporée n'est pas accessible
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "default-categories.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                _cachedCatalog = JsonSerializer.Deserialize<List<CategoryTemplateDto>>(json, DefaultJsonOptions)
                    ?? new List<CategoryTemplateDto>();
                return _cachedCatalog;
            }

            return Array.Empty<CategoryTemplateDto>();
        }
    }

    public async Task EnsureDefaultCategoriesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var existingCategories = await _dbContext.Categories
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);

        if (existingCategories.Count == 0)
        {
            await RestoreDefaultCategoriesAsync(userId, cancellationToken);
        }
        else
        {
            await EnsureMissingTranslationsAsync(existingCategories, cancellationToken);
        }
    }

    public async Task<int> RestoreDefaultCategoriesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        var templates = GetDefaultCategoryTemplates();
        if (templates.Count == 0)
        {
            _logger.LogWarning("Aucun gabarit de catégorie par défaut trouvé dans les ressources.");
            return 0;
        }

        var existingCategories = await _dbContext.Categories
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);

        var existingCatIds = existingCategories.Select(c => c.Id).ToHashSet();
        var existingTranslations = await _dbContext.EntityTranslations
            .Where(t => t.EntityType == "Category" && existingCatIds.Contains(t.EntityId))
            .ToListAsync(cancellationToken);

        var existingParents = existingCategories.Where(c => c.ParentCategoryId == null).ToList();
        var existingSubsByParent = existingCategories
            .Where(c => c.ParentCategoryId.HasValue)
            .GroupBy(c => c.ParentCategoryId.GetValueOrDefault())
            .ToDictionary(g => g.Key, g => g.ToList());

        var now = DateTime.UtcNow;
        var categoriesToAdd = new List<Category>();
        var translationsToAdd = new List<EntityTranslation>();
        var existingTranslationsMap = existingTranslations
            .Select(t => $"{t.EntityId}:{t.LanguageCode.ToLowerInvariant()}:{t.Field.ToLowerInvariant()}")
            .ToHashSet();

        int restoredCount = 0;

        foreach (var parentTemplate in templates)
        {
            var matchingParent = existingParents.FirstOrDefault(p => MatchesTemplate(p, parentTemplate, existingTranslations));
            Category parentCategory;

            if (matchingParent == null)
            {
                parentCategory = new Category
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = parentTemplate.GetDefaultName(),
                    ColorHex = parentTemplate.ColorHex,
                    IconName = parentTemplate.IconName,
                    IsIncome = parentTemplate.IsIncome,
                    IsSalary = false,
                    ParentCategoryId = null,
                    CreatedAtUtc = now
                };
                categoriesToAdd.Add(parentCategory);
                existingParents.Add(parentCategory);
                restoredCount++;

                foreach (var (lang, text) in parentTemplate.Translations)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        translationsToAdd.Add(new EntityTranslation
                        {
                            EntityType = "Category",
                            EntityId = parentCategory.Id,
                            LanguageCode = lang.ToLowerInvariant(),
                            Field = "Name",
                            Translation = text,
                            CreatedAtUtc = now
                        });
                        existingTranslationsMap.Add($"{parentCategory.Id}:{lang.ToLowerInvariant()}:name");
                    }
                }
            }
            else
            {
                parentCategory = matchingParent;
                foreach (var (lang, text) in parentTemplate.Translations)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var key = $"{parentCategory.Id}:{lang.ToLowerInvariant()}:name";
                        if (!existingTranslationsMap.Contains(key))
                        {
                            translationsToAdd.Add(new EntityTranslation
                            {
                                EntityType = "Category",
                                EntityId = parentCategory.Id,
                                LanguageCode = lang.ToLowerInvariant(),
                                Field = "Name",
                                Translation = text,
                                CreatedAtUtc = now
                            });
                            existingTranslationsMap.Add(key);
                        }
                    }
                }
            }

            if (!existingSubsByParent.TryGetValue(parentCategory.Id, out var existingSubs))
            {
                existingSubs = new List<Category>();
                existingSubsByParent[parentCategory.Id] = existingSubs;
            }

            foreach (var subTemplate in parentTemplate.SubCategories)
            {
                var matchingSub = existingSubs.FirstOrDefault(s => MatchesTemplate(s, subTemplate, existingTranslations));
                if (matchingSub == null)
                {
                    var subCategory = new Category
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Name = subTemplate.GetDefaultName(),
                        ColorHex = subTemplate.ColorHex,
                        IconName = subTemplate.IconName,
                        IsIncome = subTemplate.IsIncome,
                        IsSalary = subTemplate.IsSalary,
                        ParentCategoryId = parentCategory.Id,
                        CreatedAtUtc = now
                    };
                    categoriesToAdd.Add(subCategory);
                    existingSubs.Add(subCategory);
                    restoredCount++;

                    foreach (var (lang, text) in subTemplate.Translations)
                    {
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            translationsToAdd.Add(new EntityTranslation
                            {
                                EntityType = "Category",
                                EntityId = subCategory.Id,
                                LanguageCode = lang.ToLowerInvariant(),
                                Field = "Name",
                                Translation = text,
                                CreatedAtUtc = now
                            });
                            existingTranslationsMap.Add($"{subCategory.Id}:{lang.ToLowerInvariant()}:name");
                        }
                    }
                }
                else
                {
                    foreach (var (lang, text) in subTemplate.Translations)
                    {
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var key = $"{matchingSub.Id}:{lang.ToLowerInvariant()}:name";
                            if (!existingTranslationsMap.Contains(key))
                            {
                                translationsToAdd.Add(new EntityTranslation
                                {
                                    EntityType = "Category",
                                    EntityId = matchingSub.Id,
                                    LanguageCode = lang.ToLowerInvariant(),
                                    Field = "Name",
                                    Translation = text,
                                    CreatedAtUtc = now
                                });
                                existingTranslationsMap.Add(key);
                            }
                        }
                    }
                }
            }
        }

        if (categoriesToAdd.Count > 0)
        {
            _dbContext.Categories.AddRange(categoriesToAdd);
        }

        if (translationsToAdd.Count > 0)
        {
            _dbContext.EntityTranslations.AddRange(translationsToAdd);
        }

        if (categoriesToAdd.Count > 0 || translationsToAdd.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Restauration des catégories terminée pour {UserId} : {RestoredCount} catégories ajoutées, {TransCount} traductions.",
            userId,
            restoredCount,
            translationsToAdd.Count);

        return restoredCount;
    }

    private async Task EnsureMissingTranslationsAsync(List<Category> categories, CancellationToken cancellationToken)
    {
        var categoryIds = categories.Select(c => c.Id).ToHashSet();
        var existingTranslations = await _dbContext.EntityTranslations
            .Where(t => t.EntityType == "Category" && categoryIds.Contains(t.EntityId))
            .ToListAsync(cancellationToken);

        var existingMap = existingTranslations
            .Select(t => $"{t.EntityId}:{t.LanguageCode.ToLowerInvariant()}:{t.Field.ToLowerInvariant()}")
            .ToHashSet();

        var templates = GetDefaultCategoryTemplates();
        var flatTemplates = new List<CategoryTemplateDto>();
        void CollectTemplates(CategoryTemplateDto tpl)
        {
            flatTemplates.Add(tpl);
            foreach (var sub in tpl.SubCategories)
            {
                CollectTemplates(sub);
            }
        }

        foreach (var parent in templates)
        {
            CollectTemplates(parent);
        }

        var translationsToAdd = new List<EntityTranslation>();
        var now = DateTime.UtcNow;

        foreach (var cat in categories)
        {
            if (string.IsNullOrWhiteSpace(cat.Name))
            {
                continue;
            }

            var matchingTemplate = flatTemplates.FirstOrDefault(tpl => MatchesTemplate(cat, tpl, existingTranslations));
            if (matchingTemplate != null)
            {
                foreach (var (lang, text) in matchingTemplate.Translations)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var key = $"{cat.Id}:{lang.ToLowerInvariant()}:name";
                        if (!existingMap.Contains(key))
                        {
                            translationsToAdd.Add(new EntityTranslation
                            {
                                EntityType = "Category",
                                EntityId = cat.Id,
                                LanguageCode = lang.ToLowerInvariant(),
                                Field = "Name",
                                Translation = text,
                                CreatedAtUtc = now
                            });
                            existingMap.Add(key);
                        }
                    }
                }
            }
            else
            {
                var frKey = $"{cat.Id}:fr:name";
                if (!existingMap.Contains(frKey))
                {
                    translationsToAdd.Add(new EntityTranslation
                    {
                        EntityType = "Category",
                        EntityId = cat.Id,
                        LanguageCode = "fr",
                        Field = "Name",
                        Translation = cat.Name,
                        CreatedAtUtc = now
                    });
                    existingMap.Add(frKey);
                }
            }
        }

        if (translationsToAdd.Count > 0)
        {
            _dbContext.EntityTranslations.AddRange(translationsToAdd);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("{Count} traductions de catégories manquantes ont été ajoutées.", translationsToAdd.Count);
        }
    }

    private static bool MatchesTemplate(Category category, CategoryTemplateDto template, List<EntityTranslation> existingTranslations)
    {
        var normalizedCategoryName = NormalizeString(category.Name);
        var categoryNames = existingTranslations
            .Where(t => t.EntityId == category.Id && string.Equals(t.Field, "Name", StringComparison.OrdinalIgnoreCase))
            .Select(t => NormalizeString(t.Translation))
            .ToHashSet();

        categoryNames.Add(normalizedCategoryName);

        return template.Translations.Values.Any(templateTranslation =>
            categoryNames.Contains(NormalizeString(templateTranslation)));
    }

    private static string NormalizeString(string? text)
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
