using AwesomeAssertions;
using Ledger.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Ledger.Tests.Fixtures;

public sealed class SqliteWalConnectionInterceptorTests : IDisposable
{
    private readonly string _dbFilePath;
    private readonly SqliteConnection _connection;

    public SqliteWalConnectionInterceptorTests()
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), "ledger_wal_" + Guid.NewGuid().ToString("N") + ".db");
        _connection = new SqliteConnection($"Data Source={_dbFilePath};");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbFilePath))
        {
            try
            {
                File.Delete(_dbFilePath);
            }
            catch
            {
                // Ignore cleanup lock
            }
        }
    }

    [Fact]
    public void ConnectionOpened_WhenCalled_EnablesForeignKeysAndWal()
    {
        var interceptor = new SqliteWalConnectionInterceptor();
        _connection.Open();

        interceptor.ConnectionOpened(_connection, null!);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys;";
        var foreignKeys = Convert.ToInt64(cmd.ExecuteScalar());
        foreignKeys.Should().Be(1);
    }

    [Fact]
    public async Task ConnectionOpenedAsync_WhenCalled_EnablesForeignKeysAndWal()
    {
        var interceptor = new SqliteWalConnectionInterceptor();
        await _connection.OpenAsync();

        await interceptor.ConnectionOpenedAsync(_connection, null!, default);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys;";
        var foreignKeys = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        foreignKeys.Should().Be(1);
    }
}
