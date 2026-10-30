using AwesomeAssertions;
using Ledger.Domain.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Categories.Services;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Ledger.Tests.Features.Categories;

public sealed class CategorySeedServiceTranslationTests : IDisposable
{
    private const string UserId = "user-abc-123";

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly CategorySeedService _service;

    public CategorySeedServiceTranslationTests()
    {
        (_context, _connection) = TestDbContextFactory.Create(UserId);
        _service = new CategorySeedService(_context, NullLogger<CategorySeedService>.Instance, _timeProvider);
    }

    [Fact]
    public async Task EnsureDefaultCategoriesForUserAsync_WhenCustomCategoryHasNoTemplate_AddsFrenchTranslationWithItsNameOnce()
    {
        await _service.EnsureDefaultCategoriesForUserAsync(UserId);
        var custom = new Category { Id = Guid.NewGuid(), UserId = UserId, Name = "Ma catégorie perso" };
        _context.Categories.Add(custom);
        await _context.SaveChangesAsync();
        var fixedUtc = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero);
        _timeProvider.SetUtcNow(fixedUtc);

        await _service.EnsureDefaultCategoriesForUserAsync(UserId);
        await _service.EnsureDefaultCategoriesForUserAsync(UserId);

        var translations = await _context.EntityTranslations.Where(t => t.EntityId == custom.Id).ToListAsync();
        translations.Should().ContainSingle();
        translations[0].LanguageCode.Should().Be("fr");
        translations[0].Field.Should().Be("Name");
        translations[0].Translation.Should().Be("Ma catégorie perso");
        translations[0].CreatedAtUtc.Should().Be(fixedUtc.UtcDateTime);
    }

    [Fact]
    public async Task EnsureDefaultCategoriesForUserAsync_WhenOneTranslationExistsInOtherCase_AddsOnlyTheMissingLanguages()
    {
        await _service.EnsureDefaultCategoriesForUserAsync(UserId);
        var housing = await _context.Categories.SingleAsync(c => c.Name == "Logement, immobilier");
        var housingTranslations = await _context.EntityTranslations.Where(t => t.EntityId == housing.Id).ToListAsync();
        var expectedLanguages = housingTranslations.Select(t => t.LanguageCode).Order(StringComparer.Ordinal).ToList();
        var english = housingTranslations.Single(t => string.Equals(t.LanguageCode, "en", StringComparison.Ordinal));
        _context.EntityTranslations.RemoveRange(housingTranslations.Where(t => !string.Equals(t.LanguageCode, "fr", StringComparison.Ordinal)));
        _context.EntityTranslations.Add(new EntityTranslation { EntityType = "Category", EntityId = housing.Id, LanguageCode = "EN", Field = "Name", Translation = "Personnalisé" });
        await _context.SaveChangesAsync();

        await _service.EnsureDefaultCategoriesForUserAsync(UserId);

        var after = await _context.EntityTranslations.Where(t => t.EntityId == housing.Id).ToListAsync();
        after.Select(t => t.LanguageCode.ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Should().Equal(expectedLanguages);
        after.Where(t => t.LanguageCode.Equals("en", StringComparison.OrdinalIgnoreCase)).Should().ContainSingle()
            .Which.Translation.Should().Be("Personnalisé").And.NotBe(english.Translation);
    }

    [Fact]
    public async Task EnsureDefaultCategoriesForUserAsync_WhenCategoryNameIsBlank_AddsNoTranslation()
    {
        await _service.EnsureDefaultCategoriesForUserAsync(UserId);
        var blank = new Category { Id = Guid.NewGuid(), UserId = UserId, Name = " " };
        _context.Categories.Add(blank);
        await _context.SaveChangesAsync();

        await _service.EnsureDefaultCategoriesForUserAsync(UserId);

        (await _context.EntityTranslations.AnyAsync(t => t.EntityId == blank.Id)).Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
