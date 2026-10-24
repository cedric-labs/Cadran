using Cadran.Launcher.Configuration;

namespace Cadran.Launcher.Backups;

/// <summary>
/// Contrat de gestion et de supervision des sauvegardes automatiques et manuelles de la base SQLite.
/// </summary>
public interface IDatabaseBackupSupervisor
{
    /// <summary>
    /// Crée une sauvegarde horodatée de la base de données actuelle.
    /// </summary>
    /// <param name="reason">Motif de la sauvegarde (ex: 'Démarrage', 'Fermeture', 'Manuel').</param>
    /// <param name="retentionOptions">Options de rotation à appliquer après la création.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Le résultat de l'opération de sauvegarde.</returns>
    Task<BackupResult> CreateBackupAsync(string reason, BackupRetentionOptions? retentionOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère la liste des sauvegardes disponibles sur le disque, triées de la plus récente à la plus ancienne.
    /// </summary>
    /// <returns>Une liste en lecture seule des métadonnées de sauvegardes.</returns>
    IReadOnlyList<BackupMetadata> GetAvailableBackups();

    /// <summary>
    /// Restaure une sauvegarde sélectionnée en remplacement de la base actuelle.
    /// </summary>
    /// <param name="backupFilePath">Chemin physique du fichier de sauvegarde à restaurer.</param>
    /// <param name="isServerRunning">Indicateur d'exécution du serveur.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Le résultat de l'opération de restauration.</returns>
    Task<RestoreResult> RestoreBackupAsync(string backupFilePath, bool isServerRunning, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applique la politique de rétention configurée sur le répertoire de sauvegardes.
    /// </summary>
    /// <param name="options">Options de rétention à appliquer.</param>
    /// <returns>Le nombre de fichiers purgés.</returns>
    int ApplyRetentionPolicy(BackupRetentionOptions options);
}
