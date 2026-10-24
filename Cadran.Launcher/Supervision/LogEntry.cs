namespace Cadran.Launcher.Supervision;

/// <summary>
/// Niveaux de sévérité d'un message de journalisation Kestrel.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Trace détaillée.
    /// </summary>
    Trace = 0,

    /// <summary>
    /// Message de diagnostic.
    /// </summary>
    Debug = 1,

    /// <summary>
    /// Information générale de fonctionnement.
    /// </summary>
    Information = 2,

    /// <summary>
    /// Avertissement sans arrêt du service.
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Erreur de traitement ou exception.
    /// </summary>
    Error = 4,

    /// <summary>
    /// Défaillance critique.
    /// </summary>
    Critical = 5
}

/// <summary>
/// Entrée unitaire du journal d'exécution intercepté depuis le processus serveur.
/// </summary>
/// <param name="Timestamp">Horodatage de réception.</param>
/// <param name="Level">Niveau de sévérité déduit.</param>
/// <param name="Message">Texte brut de la ligne de journal.</param>
public readonly record struct LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Message)
{
    /// <summary>
    /// Déduit le niveau de sévérité à partir d'une ligne de journal standard ASP.NET Core / Microsoft.Extensions.Logging.
    /// </summary>
    /// <param name="line">Ligne brute de log.</param>
    /// <returns>Le niveau de sévérité déduit.</returns>
    public static LogLevel ParseLogLevel(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return LogLevel.Information;
        }

        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("crit:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("fatal:", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Critical;
        }

        if (trimmed.StartsWith("fail:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Error;
        }

        if (trimmed.StartsWith("warn:", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Warning;
        }

        if (trimmed.StartsWith("dbug:", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Debug;
        }

        if (trimmed.StartsWith("trce:", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Trace;
        }

        return LogLevel.Information;
    }
}
