using AwesomeAssertions;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Services;
using Ledger.Features.Security.Context;
using Ledger.Features.Security.Models;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ledger.Tests.Fixtures;

public sealed class CompleteDatabaseInitializationTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _connectionString;

    public CompleteDatabaseInitializationTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"ledger_test_init_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_tempDbPath};Mode=ReadWriteCreate;";
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try
            {
                SqliteConnection.ClearAllPools();
                File.Delete(_tempDbPath);
            }
            catch (IOException)
            {
                // Toléré : le fichier de base SQLite de test peut être temporairement verrouillé lors de l'arrêt du pool
            }
            catch (UnauthorizedAccessException)
            {
                // Toléré : permissions temporaires de suppression dans le répertoire temporaire de l'OS
            }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_FromScratch_CreatesAllTablesAndColumnsWithoutMissingAnything()
    {
        // Arrange: Prepare DI container as configured in production
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddDebug());
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(_connectionString);
        });

        services.AddScoped<ICurrentUserService, MockCurrentUserService>();

        services.AddIdentityCore<ApplicationUser>(options => { })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IUiSettingsService, UiSettingsService>();
        services.AddScoped<ICategorySeedService, CategorySeedService>();
        services.AddScoped<IAccountTypeService, AccountTypeService>();
        services.AddScoped<ITransactionStatusService, TransactionStatusService>();

        var serviceProvider = services.BuildServiceProvider();

        // Act: Run initialization on the brand-new, empty SQLite database
        var initAct = async () => await DatabaseInitializer.InitializeDatabaseAsync(serviceProvider);
        await initAct.Should().NotThrowAsync();

        // Assert 1: Verify all 25 application and identity tables exist in SQLite
        var expectedTables = new[]
        {
            "__EFMigrationsHistory",
            "AspNetUsers",
            "AspNetRoles",
            "AspNetUserRoles",
            "AspNetUserClaims",
            "AspNetUserLogins",
            "AspNetUserTokens",
            "AspNetRoleClaims",
            "AppSettings",
            "Banks",
            "BankAccounts",
            "Categories",
            "Merchants",
            "Transactions",
            "TransactionSplits",
            "CategorizationRules",
            "CsvMappingProfiles",
            "ImportBatches",
            "TransactionStatuses",
            "CustomAccountTypes",
            "UiFonts",
            "EntityTranslations",
            "RecurringExpenses",
            "ReportDashboards",
            "ReportWidgets",
            "SavedPivotReports"
        };

        var actualTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var conn = new SqliteConnection(_connectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    actualTables.Add(reader.GetString(0));
                }
            }

            foreach (var table in expectedTables)
            {
                actualTables.Should().Contain(table, $"La table '{table}' doit impérativement être créée pour un nouvel utilisateur.");
            }

            // Assert 2: Verify all key columns on critical tables exist
            await AssertColumnExistsAsync(conn, "Transactions", "BudgetDate");
            await AssertColumnExistsAsync(conn, "Transactions", "CustomStatusId");
            await AssertColumnExistsAsync(conn, "Transactions", "MerchantId");
            await AssertColumnExistsAsync(conn, "Transactions", "IsSplit");

            await AssertColumnExistsAsync(conn, "CategorizationRules", "ShiftToNextMonth");
            await AssertColumnExistsAsync(conn, "CategorizationRules", "TargetMerchantId");

            await AssertColumnExistsAsync(conn, "Categories", "IsSalary");

            await AssertColumnExistsAsync(conn, "BankAccounts", "CustomAccountTypeId");
            await AssertColumnExistsAsync(conn, "BankAccounts", "OfxAccountId");

            // Assert 3: Verify reference seed data
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var fontCount = await context.UiFonts.CountAsync();
                fontCount.Should().BeGreaterThan(0, "Les polices système doivent être initialisées.");

                var currencySetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Currencies:Supported");
                currencySetting.Should().NotBeNull("La configuration des devises doit être initialisée.");
            }
        }

        // Act & Assert 4: Running InitializeDatabaseAsync a second time on the same DB must be completely idempotent
        var reinitAct = async () => await DatabaseInitializer.InitializeDatabaseAsync(serviceProvider);
        await reinitAct.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SeedReferenceDataAsync_WhenTimeIsFixed_SetsCurrencySettingUpdatedAtUtcFromTimeProvider()
    {
        var fixedUtc = new DateTimeOffset(2025, 3, 15, 15, 30, 0, TimeSpan.Zero);
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(fixedUtc);
        var (context, connection) = TestDbContextFactory.Create("test-user-id", fakeTime);
        await using var _ = context;
        await using var __ = connection;

        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var seeder = new DatabaseReferenceDataSeeder(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DatabaseReferenceDataSeeder>.Instance,
            fakeTime);

        await seeder.SeedReferenceDataAsync(context, serviceProvider);

        var currencySetting = await context.AppSettings.FirstAsync(s => s.Key == "Currencies:Supported");
        currencySetting.UpdatedAtUtc.Should().Be(fixedUtc.UtcDateTime);
    }

    private static async Task AssertColumnExistsAsync(SqliteConnection conn, string tableName, string columnName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        using var reader = await cmd.ExecuteReaderAsync();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        columns.Should().Contain(columnName, $"La colonne '{columnName}' doit exister sur la table '{tableName}'.");
    }

    private class MockCurrentUserService : ICurrentUserService
    {
        public string? UserId => "test-first-time-user";
        public string? UserEmail => "firstuser@Ledger.local";
        public bool IsAuthenticated => true;
    }
}
