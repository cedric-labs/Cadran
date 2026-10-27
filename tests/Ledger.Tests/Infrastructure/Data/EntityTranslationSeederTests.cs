using AwesomeAssertions;
using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ledger.Tests.Infrastructure.Data;

public class EntityTranslationSeederTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly EntityTranslationSeeder _seeder;

    public EntityTranslationSeederTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _seeder = new EntityTranslationSeeder(NullLogger<EntityTranslationSeeder>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SeedDefaultTranslationsAsync_WhenAccountTypeExists_InsertsEnglishTranslation()
    {
        // Arrange
        var accountType = new CustomAccountType
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Compte Courant",
            Code = "Checking",
            ColorHex = "#000000",
            IconName = "bi-wallet",
            CategoryGroup = AccountCategoryGroup.Liquid,
            DisplayOrder = 1
        };
        _context.CustomAccountTypes.Add(accountType);
        await _context.SaveChangesAsync();

        // Act
        await _seeder.SeedDefaultTranslationsAsync(_context);

        // Assert
        var translation = await _context.EntityTranslations
            .FirstOrDefaultAsync(t => t.EntityType == "CustomAccountType" && t.EntityId == accountType.Id);

        translation.Should().NotBeNull();
        translation!.LanguageCode.Should().Be("en");
        translation.Field.Should().Be("Name");
        translation.Translation.Should().Be("Checking Account");
    }

    [Fact]
    public async Task SeedDefaultTranslationsAsync_WhenCategoryMatchesFrenchDictionary_InsertsEnglishTranslation()
    {
        // Arrange
        var category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Alimentation, supermarché",
            ColorHex = "#ff0000",
            IconName = "bi-cart",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        // Act
        await _seeder.SeedDefaultTranslationsAsync(_context);

        // Assert
        var translation = await _context.EntityTranslations
            .FirstOrDefaultAsync(t => t.EntityType == "Category" && t.EntityId == category.Id);

        translation.Should().NotBeNull();
        translation!.LanguageCode.Should().Be("en");
        translation.Field.Should().Be("Name");
        translation.Translation.Should().Be("Groceries & Supermarket");
    }

    [Fact]
    public async Task SeedDefaultTranslationsAsync_WhenCalledMultipleTimes_IsIdempotent()
    {
        // Arrange
        var category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Carburant",
            ColorHex = "#ff0000",
            IconName = "bi-fuel",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        // Act
        await _seeder.SeedDefaultTranslationsAsync(_context);
        await _seeder.SeedDefaultTranslationsAsync(_context);

        // Assert
        var count = await _context.EntityTranslations
            .CountAsync(t => t.EntityType == "Category" && t.EntityId == category.Id);

        count.Should().Be(1);
    }
}
