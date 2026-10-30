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
public class CategorySeedService(AppDbContext dbContext, ILogger<CategorySeedService> logger, TimeProvider timeProvider) : ICategorySeedService
{
    private static IReadOnlyList<CategoryTemplateDto>? _cachedCatalog;
    private static readonly Lock CatalogLock = new();
    private static readonly JsonSerializerOptions DefaultJsonOptions = new() { PropertyNameCaseInsensitive = true };

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

        var existingCategories = await dbContext.Categories
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (existingCategories.Count == 0)
        {
            await RestoreDefaultCategoriesAsync(userId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await EnsureMissingTranslationsAsync(existingCategories, cancellationToken).ConfigureAwait(false);
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
            logger.LogWarning("Aucun gabarit de catégorie par défaut trouvé dans les ressources.");
            return 0;
        }

        var existingCategories = await dbContext.Categories
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var existingTranslations = await LoadCategoryTranslationsAsync(existingCategories.Select(c => c.Id).ToHashSet(), cancellationToken).ConfigureAwait(false);

        var plan = new DefaultCategoryRestorePlan(userId, timeProvider.GetUtcNow().UtcDateTime, existingTranslations);
        var parents = existingCategories.Where(c => c.ParentCategoryId == null).ToList();
        var subsByParent = existingCategories
            .Where(c => c.ParentCategoryId.HasValue)
            .GroupBy(c => c.ParentCategoryId.GetValueOrDefault())
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var parentTemplate in templates)
        {
            var parent = plan.Apply(parentTemplate, parents, parentCategoryId: null);

            if (!subsByParent.TryGetValue(parent.Id, out var subs))
            {
                subs = [];
                subsByParent[parent.Id] = subs;
            }

            foreach (var subTemplate in parentTemplate.SubCategories)
            {
                plan.Apply(subTemplate, subs, parent.Id);
            }
        }

        dbContext.Categories.AddRange(plan.CategoriesToAdd);
        dbContext.EntityTranslations.AddRange(plan.TranslationsToAdd);

        if (plan.CategoriesToAdd.Count > 0 || plan.TranslationsToAdd.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation(
            "Restauration des catégories terminée pour {UserId} : {RestoredCount} catégories ajoutées, {TransCount} traductions.",
            userId,
            plan.CategoriesToAdd.Count,
            plan.TranslationsToAdd.Count);

        return plan.CategoriesToAdd.Count;
    }

    private async Task EnsureMissingTranslationsAsync(List<Category> categories, CancellationToken cancellationToken)
    {
        var existingTranslations = await LoadCategoryTranslationsAsync(categories.Select(c => c.Id).ToHashSet(), cancellationToken).ConfigureAwait(false);

        var translationsToAdd = MissingCategoryTranslationPlanner.Plan(
            categories,
            existingTranslations,
            GetDefaultCategoryTemplates(),
            timeProvider.GetUtcNow().UtcDateTime);

        if (translationsToAdd.Count > 0)
        {
            dbContext.EntityTranslations.AddRange(translationsToAdd);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("{Count} traductions de catégories manquantes ont été ajoutées.", translationsToAdd.Count);
        }
    }

    private async Task<List<EntityTranslation>> LoadCategoryTranslationsAsync(HashSet<Guid> categoryIds, CancellationToken cancellationToken)
    {
        var allCategoryTranslations = await dbContext.EntityTranslations
            .Where(t => t.EntityType == "Category")
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return allCategoryTranslations
            .Where(t => categoryIds.Contains(t.EntityId))
            .ToList();
    }
}
