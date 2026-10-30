using System.Text.RegularExpressions;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Valide les identifiants SQL injectés dans les instructions DDL, qui ne peuvent pas être paramétrées.
/// </summary>
internal static class SchemaIdentifierValidator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Indique si le nom de table, le nom de colonne et la définition de colonne ne contiennent que des caractères autorisés.
    /// </summary>
    public static bool AreSafe(string tableName, string columnName, string columnDefinition)
    {
        return Regex.IsMatch(tableName, @"^[a-zA-Z0-9_]+$", RegexOptions.None, RegexTimeout)
            && Regex.IsMatch(columnName, @"^[a-zA-Z0-9_]+$", RegexOptions.None, RegexTimeout)
            && Regex.IsMatch(columnDefinition, @"^[a-zA-Z0-9_\(\)\s'""]+$", RegexOptions.None, RegexTimeout);
    }
}
