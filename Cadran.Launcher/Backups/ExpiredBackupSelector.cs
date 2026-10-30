using Cadran.Launcher.Configuration;

namespace Cadran.Launcher.Backups;

/// <summary>
/// Désigne les sauvegardes à supprimer selon la politique de rétention : ancienneté, nombre de fichiers, ou les deux.
/// </summary>
internal static class ExpiredBackupSelector
{
    /// <summary>
    /// Retourne les sauvegardes expirées. Le plafond de nombre s'applique aux fichiers restants après l'éviction par ancienneté ;
    /// <paramref name="newestFirst"/> doit être trié de la plus récente à la plus ancienne.
    /// </summary>
    public static HashSet<FileInfo> Select(IReadOnlyList<FileInfo> newestFirst, BackupRetentionOptions options, DateTimeOffset now)
    {
        var expired = new HashSet<FileInfo>();

        if (options.Mode is BackupRetentionMode.Days or BackupRetentionMode.Both)
        {
            var cutoffDate = now.AddDays(-options.MaxBackupDays);
            foreach (var file in newestFirst.Where(f => new DateTimeOffset(f.CreationTimeUtc, TimeSpan.Zero) < cutoffDate))
            {
                expired.Add(file);
            }
        }

        if (options.Mode is BackupRetentionMode.FileCount or BackupRetentionMode.Both)
        {
            var remaining = newestFirst.Where(f => !expired.Contains(f)).ToList();
            foreach (var file in remaining.Skip(options.MaxBackupFiles))
            {
                expired.Add(file);
            }
        }

        return expired;
    }
}
