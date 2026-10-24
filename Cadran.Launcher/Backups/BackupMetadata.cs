namespace Cadran.Launcher.Backups;

/// <summary>
/// Métadonnées d'un fichier de sauvegarde de la base de données.
/// </summary>
/// <param name="FilePath">Chemin physique absolu du fichier de sauvegarde.</param>
/// <param name="FileName">Nom du fichier de sauvegarde.</param>
/// <param name="CreatedAt">Date et heure de création de la sauvegarde.</param>
/// <param name="SizeInBytes">Taille du fichier en octets.</param>
/// <param name="Reason">Motif de déclenchement de la sauvegarde.</param>
public readonly record struct BackupMetadata(
    string FilePath,
    string FileName,
    DateTimeOffset CreatedAt,
    long SizeInBytes,
    string Reason);
