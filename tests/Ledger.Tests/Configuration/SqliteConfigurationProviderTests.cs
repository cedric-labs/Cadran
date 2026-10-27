using AwesomeAssertions;
using Ledger.Infrastructure.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ledger.Tests.Configuration;

public class SqliteConfigurationProviderTests : IDisposable
{
    private readonly string _dbFilePath;
    private readonly string _connectionString;

    public SqliteConfigurationProviderTests()
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), "ledger_cfg_" + Guid.NewGuid().ToString("N") + ".db");
        _connectionString = $"Data Source={_dbFilePath};";
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        // Nettoyage de la base de test
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
    public void Load_OnEmptyDatabase_ShouldCreateAppSettingsTableAndSeedDefaults()
    {
        var provider = new SqliteConfigurationProvider(_connectionString);

        provider.Load();

        provider.TryGet("App:DefaultCurrency", out var currency).Should().BeTrue();
        currency.Should().Be("EUR");

        provider.TryGet("App:Name", out var appName).Should().BeTrue();
        appName.Should().Contain("Cadran");

        provider.TryGet("Security:MaxFailedAccessAttempts", out var attempts).Should().BeTrue();
        attempts.Should().Be("5");

        provider.TryGet("Currencies:Supported", out var supportedCurrencies).Should().BeTrue();
        supportedCurrencies.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Load_WithCustomSettings_ShouldLoadCustomValues()
    {
        // Pré-initialisation
        var provider = new SqliteConfigurationProvider(_connectionString);
        provider.Load();

        // Insertion d'un paramètre personnalisé
        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO AppSettings (Key, Value, Description, UpdatedAtUtc) VALUES ('Custom:FeatureX', 'enabled', 'Test feature', datetime('now'));";
            cmd.ExecuteNonQuery();
        }

        // Rechargement
        provider.Load();

        provider.TryGet("Custom:FeatureX", out var featureVal).Should().BeTrue();
        featureVal.Should().Be("enabled");
    }

    [Fact]
    public void ConfigurationBuilder_AddSqliteConfiguration_ShouldIntegrateSuccessfully()
    {
        var configuration = new ConfigurationBuilder()
            .AddSqliteConfiguration(_connectionString)
            .Build();

        configuration["App:DefaultCurrency"].Should().Be("EUR");
        configuration["App:Name"].Should().Contain("Cadran");
    }
}
