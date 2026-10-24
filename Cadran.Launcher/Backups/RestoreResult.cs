namespace Cadran.Launcher.Backups;

/// <summary>
/// Résultat d'une opération de restauration de base de données.
/// </summary>
/// <param name="IsSuccess">Indique si la restauration a réussi.</param>
/// <param name="ErrorMessage">Message descriptif en cas d'anomalie.</param>
public readonly record struct RestoreResult(
    bool IsSuccess,
    string? ErrorMessage)
{
    /// <summary>
    /// Crée un résultat de succès.
    /// </summary>
    public static RestoreResult Success() => new(true, null);

    /// <summary>
    /// Crée un résultat d'échec.
    /// </summary>
    /// <param name="message">Raison de l'échec.</param>
    public static RestoreResult Failure(string message) => new(false, message);
}
