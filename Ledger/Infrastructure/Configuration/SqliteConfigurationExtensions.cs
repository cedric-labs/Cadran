using Microsoft.Extensions.Configuration;

namespace Ledger.Infrastructure.Configuration;

/// <summary>
/// Méthodes d'extension pour brancher le fournisseur de configuration SQLite sur le HostBuilder.
/// </summary>
public static class SqliteConfigurationExtensions
{
    public static IConfigurationBuilder AddSqliteConfiguration(this IConfigurationBuilder builder, string connectionString)
    {
        return builder.Add(new SqliteConfigurationSource(connectionString));
    }

    public static IConfigurationBuilder AddSqliteConfiguration(this IConfigurationBuilder builder, Func<string?> connectionStringResolver)
    {
        return builder.Add(new SqliteConfigurationSource(connectionStringResolver));
    }
}
