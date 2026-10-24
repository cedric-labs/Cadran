namespace Cadran.Launcher.Configuration;

/// <summary>
/// Définit le mode de rétention pour la rotation automatique des sauvegardes de base de données.
/// </summary>
public enum BackupRetentionMode
{
    /// <summary>
    /// Rétention basée sur le nombre maximal de fichiers conservés.
    /// </summary>
    FileCount = 0,

    /// <summary>
    /// Rétention basée sur l'ancienneté maximale en jours.
    /// </summary>
    Days = 1,

    /// <summary>
    /// Rétention combinée (purge déclenchée si le nombre ou l'ancienneté dépasse les seuils fixés).
    /// </summary>
    Both = 2
}
