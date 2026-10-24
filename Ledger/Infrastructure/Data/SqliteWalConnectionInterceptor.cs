using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Intercepteur EF Core garantissant l'activation systématique du mode WAL (Write-Ahead Logging),
/// du busy_timeout et des clés étrangères pour chaque ouverture de connexion SQLite.
/// Délègue son implémentation au framework partagé <see cref="SqliteForge.Data.SqliteWalConnectionInterceptor"/>.
/// </summary>
public class SqliteWalConnectionInterceptor : DbConnectionInterceptor
{
    private readonly SqliteForge.Data.SqliteWalConnectionInterceptor _inner = new();

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        _inner.ConnectionOpened(connection, eventData);
    }

    /// <inheritdoc />
    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        return _inner.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
