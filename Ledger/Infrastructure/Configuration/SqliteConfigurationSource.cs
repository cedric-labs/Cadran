using Microsoft.Extensions.Configuration;

namespace Ledger.Infrastructure.Configuration;

/// <summary>
/// Source de configuration IConfigurationSource pour SQLite.
/// </summary>
public sealed class SqliteConfigurationSource(Func<string?> connectionStringResolver) : IConfigurationSource
{
    public SqliteConfigurationSource(string connectionString) : this(() => connectionString)
    {
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new SqliteConfigurationProvider(connectionStringResolver);
    }
}
