using AwesomeAssertions;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Ledger.Tests.Infrastructure.Data;

public sealed class StaleMigrationLockRemoverTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 13, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly StaleMigrationLockRemover _remover;

    public StaleMigrationLockRemoverTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _remover = new StaleMigrationLockRemover(NullLogger<StaleMigrationLockRemover>.Instance, new FakeTimeProvider(Now));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RemoveIfStaleAsync_WhenLockIsOlderThanFiveMinutes_RemovesItAndReturnsTrue()
    {
        CreateLockTable(Now.AddHours(-1));

        var removed = await _remover.RemoveIfStaleAsync(_context);

        removed.Should().BeTrue();
        CountLocks().Should().Be(0);
    }

    [Fact]
    public async Task RemoveIfStaleAsync_WhenLockIsRecent_KeepsItAndReturnsFalse()
    {
        CreateLockTable(Now.AddSeconds(-30));

        var removed = await _remover.RemoveIfStaleAsync(_context);

        removed.Should().BeFalse();
        CountLocks().Should().Be(1);
    }

    [Fact]
    public async Task RemoveIfStaleAsync_WhenLockTableDoesNotExist_ReturnsFalse()
    {
        var removed = await _remover.RemoveIfStaleAsync(_context);

        removed.Should().BeFalse();
    }

    private void CreateLockTable(DateTimeOffset timestamp)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE "__EFMigrationsLock" ("Id" INTEGER NOT NULL CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY, "Timestamp" TEXT NOT NULL);
            INSERT INTO "__EFMigrationsLock" ("Id", "Timestamp") VALUES (1, $timestamp);
            """;
        command.Parameters.AddWithValue("$timestamp", timestamp.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz", System.Globalization.CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    private long CountLocks()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM \"__EFMigrationsLock\"";
        return (long)(command.ExecuteScalar() ?? 0L);
    }
}
