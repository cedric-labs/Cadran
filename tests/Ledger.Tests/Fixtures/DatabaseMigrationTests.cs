using FluentAssertions;
using Ledger.Features.Security.Context;
using Ledger.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ledger.Tests.Fixtures;

public class DatabaseMigrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DatabaseMigrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task MigrateAsync_OnEmptyDatabase_AppliesAllMigrationsSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        var mockUser = new MockCurrentUserService("test-mig-user");
        using var context = new AppDbContext(options, mockUser);

        // Act
        var act = async () => await context.Database.MigrateAsync();

        // Assert
        await act.Should().NotThrowAsync();

        // Verify key tables exist
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Merchants', 'TransactionSplits', 'EntityTranslations', 'RecurringExpenses');";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        count.Should().Be(4);
    }

    [Fact]
    public async Task MigrateAsync_WhenTableEntityTranslationsAlreadyExists_DoesNotThrow()
    {
        // Pre-create EntityTranslations table manually (simulating pre-existing SQLite database)
        using (var preCmd = _connection.CreateCommand())
        {
            preCmd.CommandText = @"
                CREATE TABLE EntityTranslations (
                    Id TEXT PRIMARY KEY,
                    EntityType TEXT NOT NULL,
                    EntityId TEXT NOT NULL,
                    LanguageCode TEXT NOT NULL,
                    Field TEXT NOT NULL,
                    Translation TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT
                );";
            await preCmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        var mockUser = new MockCurrentUserService("test-mig-user-2");
        using var context = new AppDbContext(options, mockUser);

        // Act & Assert: Must not throw 'table "EntityTranslations" already exists'
        var act = async () => await context.Database.MigrateAsync();
        await act.Should().NotThrowAsync();
    }

    private class MockCurrentUserService : ICurrentUserService
    {
        public MockCurrentUserService(string userId) => UserId = userId;
        public string? UserId { get; }
        public string? UserEmail => "test@test.local";
        public bool IsAuthenticated => true;
    }
}
