using AwesomeAssertions;
using Cadran.Launcher.Backups;
using Cadran.Launcher.Configuration;

namespace Cadran.Launcher.Tests.Backups;

public sealed class DatabaseBackupSupervisorTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _databaseFilePath;
    private readonly string _backupDirectoryPath;

    public DatabaseBackupSupervisorTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "CadranBackupTests_" + Guid.NewGuid().ToString("N"));
        var dataDirectory = Path.Combine(_testDirectory, "data");
        Directory.CreateDirectory(dataDirectory);

        _databaseFilePath = Path.Combine(dataDirectory, "cadran.db");
        _backupDirectoryPath = Path.Combine(_testDirectory, "backups");
        Directory.CreateDirectory(_backupDirectoryPath);

        File.WriteAllText(_databaseFilePath, "ORIGINAL_DATABASE_CONTENT");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CreateBackupAsync_WhenDatabaseExists_CreatesTimestampedFile()
    {
        // Arrange
        var supervisor = new DatabaseBackupSupervisor(_databaseFilePath, _backupDirectoryPath, TimeProvider.System);

        // Act
        var result = await supervisor.CreateBackupAsync("test");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.BackupFilePath.Should().NotBeNull();
        File.Exists(result.BackupFilePath).Should().BeTrue();
        (await File.ReadAllTextAsync(result.BackupFilePath)).Should().Be("ORIGINAL_DATABASE_CONTENT");
    }

    [Fact]
    public async Task CreateBackupAsync_WhenDatabaseIsMissing_ReturnsFailureResult()
    {
        // Arrange
        var missingDbPath = Path.Combine(_testDirectory, "missing.db");
        var supervisor = new DatabaseBackupSupervisor(missingDbPath, _backupDirectoryPath, TimeProvider.System);

        // Act
        var result = await supervisor.CreateBackupAsync("test");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("introuvable");
    }

    [Fact]
    public async Task GetAvailableBackups_WhenBackupsExist_ReturnsOrderedList()
    {
        // Arrange
        var supervisor = new DatabaseBackupSupervisor(_databaseFilePath, _backupDirectoryPath, TimeProvider.System);
        await supervisor.CreateBackupAsync("lancement");
        await Task.Delay(1100); // Décaler le timestamp de la seconde sauvegarde
        await supervisor.CreateBackupAsync("fermeture");

        // Act
        var backups = supervisor.GetAvailableBackups();

        // Assert
        backups.Should().HaveCount(2);
        backups[0].Reason.Should().Be("fermeture");
        backups[1].Reason.Should().Be("lancement");
    }

    [Fact]
    public async Task RestoreBackupAsync_WhenServerIsRunning_ReturnsFailureWithoutModifyingDatabase()
    {
        // Arrange
        var supervisor = new DatabaseBackupSupervisor(_databaseFilePath, _backupDirectoryPath, TimeProvider.System);
        var backup = await supervisor.CreateBackupAsync("initial");
        await File.WriteAllTextAsync(_databaseFilePath, "MODIFIED_CONTENT");

        // Act
        var result = await supervisor.RestoreBackupAsync(backup.BackupFilePath!, isServerRunning: true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("en cours d'exécution");
        (await File.ReadAllTextAsync(_databaseFilePath)).Should().Be("MODIFIED_CONTENT");
    }

    [Fact]
    public async Task RestoreBackupAsync_WhenBackupIsValid_ReplacesDatabaseAndCleansWalFiles()
    {
        // Arrange
        var supervisor = new DatabaseBackupSupervisor(_databaseFilePath, _backupDirectoryPath, TimeProvider.System);
        var backup = await supervisor.CreateBackupAsync("checkpoint");

        // Simuler des modifications et l'apparition de fichiers WAL et SHM
        await File.WriteAllTextAsync(_databaseFilePath, "CORRUPTED_OR_NEW_CONTENT");
        var walFile = _databaseFilePath + "-wal";
        var shmFile = _databaseFilePath + "-shm";
        await File.WriteAllTextAsync(walFile, "TEMP_WAL");
        await File.WriteAllTextAsync(shmFile, "TEMP_SHM");

        // Act
        var result = await supervisor.RestoreBackupAsync(backup.BackupFilePath!, isServerRunning: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await File.ReadAllTextAsync(_databaseFilePath)).Should().Be("ORIGINAL_DATABASE_CONTENT");
        File.Exists(walFile).Should().BeFalse();
        File.Exists(shmFile).Should().BeFalse();
    }

    [Fact]
    public void ApplyRetentionPolicy_WhenMaxFilesExceeded_PurgesOldestFiles()
    {
        // Arrange
        var supervisor = new DatabaseBackupSupervisor(_databaseFilePath, _backupDirectoryPath, TimeProvider.System);

        // Créer manuellement 5 fichiers de sauvegarde avec des dates échelonnées
        for (var i = 1; i <= 5; i++)
        {
            var fakeFile = Path.Combine(_backupDirectoryPath, $"cadran_backup_2026102{i}_120000_test.db");
            File.WriteAllText(fakeFile, "DUMMY");
            File.SetCreationTimeUtc(fakeFile, DateTime.UtcNow.AddMinutes(i));
        }

        var options = new BackupRetentionOptions(
            Mode: BackupRetentionMode.FileCount,
            MaxBackupFiles: 3,
            MaxBackupDays: 365);

        // Act
        var deletedCount = supervisor.ApplyRetentionPolicy(options);

        // Assert
        deletedCount.Should().Be(2);
        var remaining = supervisor.GetAvailableBackups();
        remaining.Should().HaveCount(3);
    }

    [Fact]
    public void ApplyRetentionPolicy_WhenMaxDaysExceeded_PurgesExpiredFiles()
    {
        // Arrange
        var supervisor = new DatabaseBackupSupervisor(_databaseFilePath, _backupDirectoryPath, TimeProvider.System);

        // Créer un fichier vieux de 40 jours et un fichier récent
        var expiredFile = Path.Combine(_backupDirectoryPath, "cadran_backup_20260901_120000_old.db");
        var freshFile = Path.Combine(_backupDirectoryPath, "cadran_backup_20261020_120000_recent.db");

        File.WriteAllText(expiredFile, "OLD");
        File.SetCreationTimeUtc(expiredFile, DateTime.UtcNow.AddDays(-40));

        File.WriteAllText(freshFile, "RECENT");
        File.SetCreationTimeUtc(freshFile, DateTime.UtcNow.AddDays(-2));

        var options = new BackupRetentionOptions(
            Mode: BackupRetentionMode.Days,
            MaxBackupFiles: 50,
            MaxBackupDays: 15);

        // Act
        var deletedCount = supervisor.ApplyRetentionPolicy(options);

        // Assert
        deletedCount.Should().Be(1);
        File.Exists(expiredFile).Should().BeFalse();
        File.Exists(freshFile).Should().BeTrue();
    }

    [Fact]
    public async Task CreateBackupAsync_WhenTimeIsFixed_NamesBackupWithTimestamp()
    {
        var fixedUtc = new DateTimeOffset(2025, 3, 15, 8, 5, 9, TimeSpan.Zero);
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(fixedUtc);
        var supervisor = new DatabaseBackupSupervisor(_databaseFilePath, _backupDirectoryPath, fakeTime);

        var result = await supervisor.CreateBackupAsync("manual");

        result.IsSuccess.Should().BeTrue();
        Path.GetFileName(result.BackupFilePath).Should().Be("cadran_backup_20250315_080509_manual.db");
    }
}
