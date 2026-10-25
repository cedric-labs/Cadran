using Ledger.Features.Categories.Services;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ledger.Tests.Features.Categories;

public class CategorySeedServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly CategorySeedService _service;
    private const string TestUserId = "user-abc-123";

    public CategorySeedServiceTests()
    {
        var (context, connection) = TestDbContextFactory.Create(TestUserId);
        _context = context;
        _connection = connection;
        _service = new CategorySeedService(_context, NullLogger<CategorySeedService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EnsureDefaultCategoriesForUser_WhenUserIdIsNullOrWhitespace_DoesNothing(string? userId)
    {
        await _service.EnsureDefaultCategoriesForUserAsync(userId!);
        var count = await _context.Categories.IgnoreQueryFilters().CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task EnsureDefaultCategoriesForUser_WhenEmpty_SeedsCompleteHierarchyAndTranslations()
    {
        // Act
        await _service.EnsureDefaultCategoriesForUserAsync(TestUserId);

        // Assert
        var allCategories = await _context.Categories.IgnoreQueryFilters().Where(c => c.UserId == TestUserId).ToListAsync();
        Assert.Equal(115, allCategories.Count);

        var parents = allCategories.Where(c => c.ParentCategoryId == null).ToList();
        Assert.Equal(12, parents.Count);

        var parentIds = parents.Select(p => p.Id).ToHashSet();
        var children = allCategories.Where(c => c.ParentCategoryId != null).ToList();
        Assert.Equal(103, children.Count);
        Assert.All(children, c => Assert.Contains(c.ParentCategoryId!.Value, parentIds));

        // Vérifier le flag salaire
        var salaryCat = allCategories.FirstOrDefault(c => c.IsSalary);
        Assert.NotNull(salaryCat);
        Assert.Contains("Salaire", salaryCat.Name);

        // Vérifier les traductions créées
        var catIds = allCategories.Select(c => c.Id).ToHashSet();
        var translations = await _context.EntityTranslations
            .Where(t => t.EntityType == "Category" && catIds.Contains(t.EntityId))
            .ToListAsync();

        var frTranslations = translations.Where(t => t.LanguageCode == "fr").ToList();
        var enTranslations = translations.Where(t => t.LanguageCode == "en").ToList();
        var esTranslations = translations.Where(t => t.LanguageCode == "es").ToList();
        var deTranslations = translations.Where(t => t.LanguageCode == "de").ToList();

        Assert.Equal(115, frTranslations.Count);
        Assert.Equal(115, enTranslations.Count);
        Assert.Equal(115, esTranslations.Count);
        Assert.Equal(115, deTranslations.Count);

        // Vérifier une traduction spécifique
        var housingParent = parents.First(p => p.Name == "Logement, immobilier");
        var housingEn = enTranslations.First(t => t.EntityId == housingParent.Id);
        var housingEs = esTranslations.First(t => t.EntityId == housingParent.Id);
        var housingDe = deTranslations.First(t => t.EntityId == housingParent.Id);
        Assert.Equal("Housing & Real Estate", housingEn.Translation);
        Assert.Equal("Vivienda e inmobiliaria", housingEs.Translation);
        Assert.Equal("Wohnen & Immobilien", housingDe.Translation);
    }

    [Fact]
    public async Task EnsureDefaultCategoriesForUser_WhenAlreadyPresent_DoesNotDuplicate()
    {
        // Act
        await _service.EnsureDefaultCategoriesForUserAsync(TestUserId);
        await _service.EnsureDefaultCategoriesForUserAsync(TestUserId);

        // Assert
        var allCategories = await _context.Categories.IgnoreQueryFilters().Where(c => c.UserId == TestUserId).ToListAsync();
        Assert.Equal(115, allCategories.Count);
    }

    [Fact]
    public async Task EnsureDefaultCategoriesForUser_WhenCategoriesExistWithoutTranslations_FillsMissingTranslations()
    {
        // Arrange : insérer d'abord les catégories sans traductions
        await _service.EnsureDefaultCategoriesForUserAsync(TestUserId);

        // Supprimer toutes les traductions existantes
        _context.EntityTranslations.RemoveRange(_context.EntityTranslations);
        await _context.SaveChangesAsync();

        Assert.Empty(await _context.EntityTranslations.ToListAsync());

        // Act
        await _service.EnsureDefaultCategoriesForUserAsync(TestUserId);

        // Assert : Les traductions ont été restaurées sans recréer de catégories supplémentaires
        var categoriesCount = await _context.Categories.IgnoreQueryFilters().Where(c => c.UserId == TestUserId).CountAsync();
        Assert.Equal(115, categoriesCount);

        var translations = await _context.EntityTranslations.ToListAsync();
        Assert.NotEmpty(translations);
        Assert.Contains(translations, t => t.LanguageCode == "en" && t.Translation == "Housing & Real Estate");
        Assert.Contains(translations, t => t.LanguageCode == "fr" && t.Translation == "Logement, immobilier");
        Assert.Contains(translations, t => t.LanguageCode == "es" && t.Translation == "Vivienda e inmobiliaria");
        Assert.Contains(translations, t => t.LanguageCode == "de" && t.Translation == "Wohnen & Immobilien");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RestoreDefaultCategories_WhenUserIdIsNullOrWhitespace_ReturnsZero(string? userId)
    {
        var count = await _service.RestoreDefaultCategoriesAsync(userId!);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task RestoreDefaultCategories_WhenEmpty_RestoresAllAndReturnsCount()
    {
        var restored = await _service.RestoreDefaultCategoriesAsync(TestUserId);

        Assert.Equal(115, restored);
        var totalInDb = await _context.Categories.IgnoreQueryFilters().Where(c => c.UserId == TestUserId).CountAsync();
        Assert.Equal(115, totalInDb);
    }

    [Fact]
    public async Task RestoreDefaultCategories_WhenAlreadyPresent_ReturnsZeroAndDoesNotDuplicate()
    {
        // Initialiser une première fois
        await _service.EnsureDefaultCategoriesForUserAsync(TestUserId);

        // Recharger les catégories par défaut
        var restored = await _service.RestoreDefaultCategoriesAsync(TestUserId);

        Assert.Equal(0, restored);
        var totalInDb = await _context.Categories.IgnoreQueryFilters().Where(c => c.UserId == TestUserId).CountAsync();
        Assert.Equal(115, totalInDb);
    }

    [Fact]
    public async Task RestoreDefaultCategories_WhenSomeDeleted_OnlyRestoresDeletedWithoutDuplicating()
    {
        // 1. Initialiser les 115 catégories
        await _service.EnsureDefaultCategoriesForUserAsync(TestUserId);

        // 2. Supprimer 2 sous-catégories spécifiques ("Entretien chaudière" et "Eau")
        var toDelete = await _context.Categories
            .Where(c => c.UserId == TestUserId && (c.Name == "Entretien chaudière" || c.Name == "Eau"))
            .ToListAsync();
        Assert.Equal(2, toDelete.Count);
        _context.Categories.RemoveRange(toDelete);
        await _context.SaveChangesAsync();

        var countAfterDelete = await _context.Categories.IgnoreQueryFilters().Where(c => c.UserId == TestUserId).CountAsync();
        Assert.Equal(113, countAfterDelete);

        // 3. Restaurer les catégories par défaut
        var restored = await _service.RestoreDefaultCategoriesAsync(TestUserId);

        // Seules les 2 supprimées ont été réintégrées
        Assert.Equal(2, restored);
        var countAfterRestore = await _context.Categories.IgnoreQueryFilters().Where(c => c.UserId == TestUserId).CountAsync();
        Assert.Equal(115, countAfterRestore);

        // Vérifier que les sous-catégories restaurées sont bien rattachées au parent "Logement, immobilier"
        var housingParent = await _context.Categories.FirstAsync(c => c.UserId == TestUserId && c.Name == "Logement, immobilier");
        var restoredSub = await _context.Categories.FirstAsync(c => c.UserId == TestUserId && c.Name == "Entretien chaudière");
        Assert.Equal(housingParent.Id, restoredSub.ParentCategoryId);
    }

    [Fact]
    public void CategoryTemplateDto_GetDefaultName_CoversAllFallbackBranches()
    {
        // 1. "fr" présent et non-vide
        var dtoFr = new CategoryTemplateDto
        {
            Translations = { ["fr"] = "Alimentation", ["en"] = "Food", ["es"] = "Comida" }
        };
        Assert.Equal("Alimentation", dtoFr.GetDefaultName());

        // 2. "fr" absent, "en" présent
        var dtoEn = new CategoryTemplateDto
        {
            Translations = { ["en"] = "Health", ["es"] = "Salud" }
        };
        Assert.Equal("Health", dtoEn.GetDefaultName());

        // 3. "fr" présent mais whitespace, "en" présent
        var dtoFrWhitespace = new CategoryTemplateDto
        {
            Translations = { ["fr"] = "   ", ["en"] = "Health" }
        };
        Assert.Equal("Health", dtoFrWhitespace.GetDefaultName());

        // 4. "fr" et "en" absents ou whitespace, autre langue disponible
        var dtoOther = new CategoryTemplateDto
        {
            Translations = { ["de"] = "Wohnen" }
        };
        Assert.Equal("Wohnen", dtoOther.GetDefaultName());

        var dtoEnWhitespace = new CategoryTemplateDto
        {
            Translations = { ["fr"] = "", ["en"] = "  ", ["de"] = "Wohnen" }
        };
        Assert.Equal("Wohnen", dtoEnWhitespace.GetDefaultName());

        // 5. Aucune traduction ou uniquement des chaînes vides -> fallback "Catégorie"
        var dtoEmpty = new CategoryTemplateDto();
        Assert.Equal("Catégorie", dtoEmpty.GetDefaultName());

        var dtoAllWhitespace = new CategoryTemplateDto
        {
            Translations = { ["fr"] = " ", ["en"] = "", ["it"] = "   " }
        };
        Assert.Equal("Catégorie", dtoAllWhitespace.GetDefaultName());
    }
}
