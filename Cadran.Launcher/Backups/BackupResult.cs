namespace Cadran.Launcher.Backups;

/// <summary>
/// Résultat d'une opération de sauvegarde de base de données.
/// </summary>
/// <param name="IsSuccess">Indique si la sauvegarde a réussi.</param>
/// <param name="BackupFilePath">Chemin physique du fichier généré si succès.</param>
/// <param name="ErrorMessage">Message descriptif en cas d'anomalie.</param>
public readonly record struct BackupResult(
    bool IsSuccess,
    string? BackupFilePath,
    string? ErrorMessage)
{
    /// <summary>
    /// Crée un résultat de succès.
    /// </summary>
    /// <param name="filePath">Chemin physique de la sauvegarde créée.</param>
    public static BackupResult Success(string filePath) => new(true, filePath, null);

    /// <summary>
    /// Crée un résultat d'échec.
    /// </summary>
    /// <param name="message">Raison de l'échec.</param>
    public static BackupResult Failure(string message) => new(false, null, message);
}
