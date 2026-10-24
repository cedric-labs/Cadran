namespace Cadran.Launcher.Configuration;

/// <summary>
/// Paramètres de rétention et de rotation des fichiers de sauvegarde.
/// </summary>
/// <param name="Mode">Stratégie d'éviction appliquée lors de la rotation.</param>
/// <param name="MaxBackupFiles">Nombre maximal de fichiers à conserver.</param>
/// <param name="MaxBackupDays">Ancienneté maximale en jours au-delà de laquelle un fichier est purgé.</param>
public sealed record BackupRetentionOptions(
    BackupRetentionMode Mode = BackupRetentionMode.FileCount,
    int MaxBackupFiles = 15,
    int MaxBackupDays = 30)
{
    /// <summary>
    /// Valide la cohérence des seuils de rétention.
    /// </summary>
    /// <returns>Une instance validée garantissant des seuils strictement positifs.</returns>
    public BackupRetentionOptions Normalize()
    {
        var sanitizedFiles = Math.Clamp(MaxBackupFiles, 1, 500);
        var sanitizedDays = Math.Clamp(MaxBackupDays, 1, 3650);
        return new BackupRetentionOptions(Mode, sanitizedFiles, sanitizedDays);
    }
}
