using AwesomeAssertions;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Categories.Services;
using Ledger.Features.Security.Models;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Ledger.Tests.Infrastructure.Data;

public sealed class DatabaseReferenceDataSeederTests : IDisposable
{
    private const string CurrenciesKey = "Currencies:Supported";

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly DatabaseReferenceDataSeeder _seeder;

    public DatabaseReferenceDataSeederTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _seeder = new DatabaseReferenceDataSeeder(NullLogger<DatabaseReferenceDataSeeder>.Instance, _timeProvider);
    }

    [Fact]
    public async Task SeedReferenceDataAsync_WhenCategoryNamesMentionSalary_FlagsOnlyThoseCategories()
    {
        _context.Categories.AddRange(
            new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Salaire net" },
            new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Revenus d'activité" },
            new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Loisirs" });
        await _context.SaveChangesAsync();

        await _seeder.SeedReferenceDataAsync(_context, new ServiceCollection().BuildServiceProvider());

        var flagged = await _context.Categories.IgnoreQueryFilters().Where(c => c.IsSalary).Select(c => c.Name).ToListAsync();
        flagged.Should().BeEquivalentTo("Salaire net", "Revenus d'activité");
    }

    [Fact]
    public async Task SeedReferenceDataAsync_WhenCurrenciesSettingIsMissing_CreatesItOnce()
    {
        var fixedUtc = new DateTimeOffset(2026, 4, 2, 8, 0, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(fixedUtc);
        var services = new ServiceCollection().BuildServiceProvider();

        await _seeder.SeedReferenceDataAsync(_context, services);
        await _seeder.SeedReferenceDataAsync(_context, services);

        var settings = await _context.AppSettings.Where(s => s.Key == CurrenciesKey).ToListAsync();
        settings.Should().ContainSingle();
        settings[0].Value.Should().Contain("EUR");
        settings[0].UpdatedAtUtc.Should().Be(fixedUtc.UtcDateTime);
    }

    [Fact]
    public async Task SeedReferenceDataAsync_WhenCurrenciesSettingExists_KeepsItsValue()
    {
        _context.AppSettings.Add(new AppSettingEntity { Key = CurrenciesKey, Value = "[]", Description = "existant" });
        await _context.SaveChangesAsync();

        await _seeder.SeedReferenceDataAsync(_context, new ServiceCollection().BuildServiceProvider());

        var setting = await _context.AppSettings.SingleAsync(s => s.Key == CurrenciesKey);
        setting.Value.Should().Be("[]");
    }

    [Fact]
    public async Task SeedReferenceDataAsync_WhenServicesAndUsersExist_DeduplicatesThenSeedsEachUser()
    {
        _context.Users.AddRange(
            new ApplicationUser { Id = "u1", UserName = "u1", NormalizedUserName = "U1" },
            new ApplicationUser { Id = "u2", UserName = "u2", NormalizedUserName = "U2" });
        await _context.SaveChangesAsync();
        var uiSettings = new Mock<IUiSettingsService>();
        var categories = new Mock<ICategorySeedService>();
        var accountTypes = new Mock<IAccountTypeService>();
        var statuses = new Mock<ITransactionStatusService>();
        var provider = new ServiceCollection()
            .AddSingleton(_context)
            .AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<AppDbContext>().Services
            .AddSingleton(uiSettings.Object)
            .AddSingleton(categories.Object)
            .AddSingleton(accountTypes.Object)
            .AddSingleton(statuses.Object)
            .BuildServiceProvider();

        await _seeder.SeedReferenceDataAsync(_context, provider);

        uiSettings.Verify(s => s.EnsureDefaultFontsAndSettingsAsync(It.IsAny<CancellationToken>()), Times.Once);
        accountTypes.Verify(s => s.DeduplicateAccountTypesAsync(It.IsAny<CancellationToken>()), Times.Once);
        statuses.Verify(s => s.DeduplicateStatusesAsync(It.IsAny<CancellationToken>()), Times.Once);
        foreach (var userId in new[] { "u1", "u2" })
        {
            categories.Verify(s => s.EnsureDefaultCategoriesForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
            accountTypes.Verify(s => s.EnsureDefaultAccountTypesForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
            statuses.Verify(s => s.EnsureDefaultStatusesForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public Task SeedReferenceDataAsync_WhenNoOptionalServiceIsRegistered_DoesNotThrow()
    {
        var act = async () => await _seeder.SeedReferenceDataAsync(_context, new ServiceCollection().BuildServiceProvider());

        return act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
