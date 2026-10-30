using AwesomeAssertions;
using Ledger.Features.Security.Context;
using Ledger.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ledger.Tests.Infrastructure.Data;

public sealed class DatabaseSchemaSynchronizerTests : IDisposable
{
    private static readonly string[] ComplementaryTables =
    [
        "Merchants", "TransactionSplits", "CustomAccountTypes", "TransactionStatuses", "UiFonts",
        "EntityTranslations", "RecurringExpenses", "ReportDashboards", "ReportWidgets", "SavedPivotReports"
    ];

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly DatabaseSchemaSynchronizer _synchronizer = new(NullLogger<DatabaseSchemaSynchronizer>.Instance);

    public DatabaseSchemaSynchronizerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _context = new AppDbContext(options, new StubCurrentUserService());
    }

    [Fact]
    public async Task SynchronizeSchemaAsync_WhenDatabaseIsEmpty_AppliesMigrationsAndCreatesComplementaryTables()
    {
        await _synchronizer.SynchronizeSchemaAsync(_context);

        foreach (var table in ComplementaryTables)
        {
            Scalar($"SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = '{table}'").Should().Be(1, table);
        }

        Scalar("SELECT COUNT(1) FROM pragma_table_info('Categories') WHERE name = 'IsSalary'").Should().Be(1);
        Scalar("SELECT COUNT(1) FROM __EFMigrationsHistory").Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SynchronizeSchemaAsync_WhenSchemaExistsWithoutMigrationHistory_RestoresMissingColumnsTablesAndHistory()
    {
        await _context.Database.EnsureCreatedAsync();
        Execute("ALTER TABLE Categories DROP COLUMN IsSalary");
        Execute("DROP TABLE RecurringExpenses");
        Execute("DROP TABLE SavedPivotReports");

        await _synchronizer.SynchronizeSchemaAsync(_context);

        Scalar("SELECT COUNT(1) FROM pragma_table_info('Categories') WHERE name = 'IsSalary'").Should().Be(1);
        Scalar("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'RecurringExpenses'").Should().Be(1);
        Scalar("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'SavedPivotReports'").Should().Be(1);
        Scalar("SELECT COUNT(1) FROM __EFMigrationsHistory").Should().Be(_context.Database.GetMigrations().Count());
    }

    [Fact]
    public async Task SynchronizeSchemaAsync_WhenCalledTwice_DoesNotThrow()
    {
        await _synchronizer.SynchronizeSchemaAsync(_context);

        var act = async () => await _synchronizer.SynchronizeSchemaAsync(_context);

        await act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private long Scalar(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        public string? UserId => "test-user-id";
        public string? UserEmail => "test@Ledger.local";
        public bool IsAuthenticated => true;
    }
}
