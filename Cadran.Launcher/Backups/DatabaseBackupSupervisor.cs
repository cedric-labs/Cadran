using System.Globalization;
using Cadran.Launcher.Configuration;

namespace Cadran.Launcher.Backups;

/// <summary>
/// Gestionnaire d'exécution des sauvegardes et des restaurations de la base SQLite.
/// </summary>
/// <param name="databaseFilePath">Chemin d'accès au fichier de base SQLite principal.</param>
/// <param name="backupDirectoryPath">Chemin du répertoire dédié aux sauvegardes.</param>
public sealed class DatabaseBackupSupervisor(string databaseFilePath, string backupDirectoryPath) : IDatabaseBackupSupervisor
{
    private const string BackupFilePrefix = "cadran_backup_";
    private const string SafetyBackupFilePrefix = "cadran_safety_";
    private const string BackupExtension = ".db";

    /// <inheritdoc />
    public async Task<BackupResult> CreateBackupAsync(
        string reason,
        BackupRetentionOptions? retentionOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(databaseFilePath))
        {
            return BackupResult.Failure("Le fichier de base de données source est introuvable. Aucune sauvegarde à créer.");
        }

        try
        {
            if (!Directory.Exists(backupDirectoryPath))
            {
                Directory.CreateDirectory(backupDirectoryPath);
            }

            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var sanitizedReason = string.IsNullOrWhiteSpace(reason) ? "manual" : reason.Trim().ToLowerInvariant();
            var targetFileName = $"{BackupFilePrefix}{timestamp}_{sanitizedReason}{BackupExtension}";
            var targetFilePath = Path.Combine(backupDirectoryPath, targetFileName);

            // Copie non-bloquante autorisant la lecture partagée en cas de mode WAL
            await CopyDatabaseFileAsync(databaseFilePath, targetFilePath, cancellationToken);

            if (retentionOptions is not null)
            {
                ApplyRetentionPolicy(retentionOptions);
            }

            return BackupResult.Success(targetFilePath);
        }
        catch (IOException ex)
        {
            return BackupResult.Failure($"Échec d'E/S lors de la création de la sauvegarde : {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return BackupResult.Failure($"Accès refusé lors de la création de la sauvegarde : {ex.Message}");
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<BackupMetadata> GetAvailableBackups()
    {
        if (!Directory.Exists(backupDirectoryPath))
        {
            return [];
        }

        var directoryInfo = new DirectoryInfo(backupDirectoryPath);
        var files = directoryInfo.GetFiles($"*{BackupExtension}");

        var result = new List<BackupMetadata>(files.Length);

        foreach (var file in files.OrderByDescending(f => f.CreationTimeUtc))
        {
            var reason = ExtractReasonFromFileName(file.Name);
            result.Add(new BackupMetadata(
                file.FullName,
                file.Name,
                new DateTimeOffset(file.CreationTimeUtc, TimeSpan.Zero),
                file.Length,
                reason));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<RestoreResult> RestoreBackupAsync(
        string backupFilePath,
        bool isServerRunning,
        CancellationToken cancellationToken = default)
    {
        if (isServerRunning)
        {
            return RestoreResult.Failure("Impossible de restaurer la base de données : le serveur Cadran est en cours d'exécution. Veuillez d'abord l'arrêter.");
        }

        if (!File.Exists(backupFilePath))
        {
            return RestoreResult.Failure("Le fichier de sauvegarde spécifié est introuvable sur le disque.");
        }

        try
        {
            // Sauvegarde de sécurité préalable de la base courante avant écrasement
            if (File.Exists(databaseFilePath))
            {
                if (!Directory.Exists(backupDirectoryPath))
                {
                    Directory.CreateDirectory(backupDirectoryPath);
                }

                var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                var safetyPath = Path.Combine(backupDirectoryPath, $"{SafetyBackupFilePrefix}{timestamp}_before_restore{BackupExtension}");
                await CopyDatabaseFileAsync(databaseFilePath, safetyPath, cancellationToken);
            }

            var targetDirectory = Path.GetDirectoryName(databaseFilePath);
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Écrasement par le fichier de restauration
            await CopyDatabaseFileAsync(backupFilePath, databaseFilePath, cancellationToken);

            // Nettoyage impératif des journaux temporaires SQLite afin de prévenir toute divergence transactionnelle
            PurgeTemporaryWalFiles(databaseFilePath);

            return RestoreResult.Success();
        }
        catch (IOException ex)
        {
            return RestoreResult.Failure($"Erreur d'E/S lors de la restauration de la base : {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return RestoreResult.Failure($"Droits insuffisants pour restaurer la base : {ex.Message}");
        }
    }

    /// <inheritdoc />
    public int ApplyRetentionPolicy(BackupRetentionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Directory.Exists(backupDirectoryPath))
        {
            return 0;
        }

        var normalizedOptions = options.Normalize();
        var directoryInfo = new DirectoryInfo(backupDirectoryPath);
        var backupFiles = directoryInfo
            .GetFiles($"{BackupFilePrefix}*{BackupExtension}")
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

        var filesToDelete = new HashSet<FileInfo>();

        // Éviction par ancienneté
        if (normalizedOptions.Mode is BackupRetentionMode.Days or BackupRetentionMode.Both)
        {
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-normalizedOptions.MaxBackupDays);
            foreach (var file in backupFiles)
            {
                if (new DateTimeOffset(file.CreationTimeUtc, TimeSpan.Zero) < cutoffDate)
                {
                    filesToDelete.Add(file);
                }
            }
        }

        // Éviction par nombre de fichiers
        if (normalizedOptions.Mode is BackupRetentionMode.FileCount or BackupRetentionMode.Both)
        {
            var remainingAfterDateFilter = backupFiles.Where(f => !filesToDelete.Contains(f)).ToList();
            if (remainingAfterDateFilter.Count > normalizedOptions.MaxBackupFiles)
            {
                var excess = remainingAfterDateFilter.Skip(normalizedOptions.MaxBackupFiles);
                foreach (var file in excess)
                {
                    filesToDelete.Add(file);
                }
            }
        }

        var deletedCount = 0;
        foreach (var file in filesToDelete)
        {
            try
            {
                file.Delete();
                deletedCount++;
            }
            catch (IOException)
            {
                // Tolérer un fichier temporairement verrouillé par un processus tiers
            }
            catch (UnauthorizedAccessException)
            {
                // Tolérer un fichier protégé en écriture
            }
        }

        return deletedCount;
    }

    private static async Task CopyDatabaseFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        const int bufferSize = 65536;
        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, useAsync: true);
        await using var destinationStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
    }

    private static void PurgeTemporaryWalFiles(string baseDbPath)
    {
        var walPath = $"{baseDbPath}-wal";
        if (File.Exists(walPath))
        {
            File.Delete(walPath);
        }

        var shmPath = $"{baseDbPath}-shm";
        if (File.Exists(shmPath))
        {
            File.Delete(shmPath);
        }
    }

    private static string ExtractReasonFromFileName(string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var parts = nameWithoutExt.Split('_');
        return parts.Length >= 4 ? parts[^1] : "automatique";
    }
}
