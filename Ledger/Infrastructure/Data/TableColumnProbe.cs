using System.Data.Common;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Interroge le catalogue SQLite pour savoir si une table et l'une de ses colonnes existent.
/// </summary>
internal static class TableColumnProbe
{
    private const string ProbeSql = @"
                    SELECT
                        (SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @tableName),
                        (SELECT COUNT(1) FROM pragma_table_info(@tableName) WHERE LOWER(name) = LOWER(@columnName));";

    /// <summary>
    /// Retourne l'existence de la table et celle de la colonne ; la connexion doit déjà être ouverte.
    /// </summary>
    public static async Task<(bool TableExists, bool ColumnExists)> ProbeAsync(DbConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = ProbeSql;
            AddParameter(command, "@tableName", tableName);
            AddParameter(command, "@columnName", columnName);

            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return (false, false);
                }

                return (reader.GetInt32(0) > 0, reader.GetInt32(1) > 0);
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
