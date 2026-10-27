using AwesomeAssertions;
using Cadran.Launcher.Configuration;

namespace Cadran.Launcher.Tests.Configuration;

public sealed class LauncherConfigurationStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configFilePath;

    public LauncherConfigurationStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "CadranTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
        _configFilePath = Path.Combine(_testDirectory, "launcher.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadSettings_WhenFileIsMissing_ReturnsDefaultValues()
    {
        // Arrange
        var store = new LauncherConfigurationStore(_configFilePath);

        // Act
        var settings = store.LoadSettings();

        // Assert
        settings.Should().NotBeNull();
        settings.Port.Should().Be(5000);
        settings.AutoStartServer.Should().BeTrue();
        settings.AutoOpenBrowser.Should().BeTrue();
        settings.EnableAutomaticBackups.Should().BeTrue();
        settings.RetentionPolicy.MaxBackupFiles.Should().Be(15);
        settings.RetentionPolicy.MaxBackupDays.Should().Be(30);
    }

    [Fact]
    public void SaveSettings_WhenSettingsAreProvided_PersistsAndReloadsCorrectly()
    {
        // Arrange
        var store = new LauncherConfigurationStore(_configFilePath);
        var custom = new LauncherSettings(
            Port: 5055,
            AutoStartServer: false,
            AutoOpenBrowser: false,
            StartMinimizedInTray: true,
            EnableAutomaticBackups: true,
            RetentionPolicy: new BackupRetentionOptions(
                Mode: BackupRetentionMode.Both,
                MaxBackupFiles: 25,
                MaxBackupDays: 60));

        // Act
        store.SaveSettings(custom);
        var loaded = store.LoadSettings();

        // Assert
        loaded.Port.Should().Be(5055);
        loaded.AutoStartServer.Should().BeFalse();
        loaded.AutoOpenBrowser.Should().BeFalse();
        loaded.StartMinimizedInTray.Should().BeTrue();
        loaded.RetentionPolicy.Mode.Should().Be(BackupRetentionMode.Both);
        loaded.RetentionPolicy.MaxBackupFiles.Should().Be(25);
        loaded.RetentionPolicy.MaxBackupDays.Should().Be(60);
    }

    [Fact]
    public void LoadSettings_WhenFileIsCorrupted_ReturnsFallbackDefaults()
    {
        // Arrange
        File.WriteAllText(_configFilePath, "{ invalid json content !!!");
        var store = new LauncherConfigurationStore(_configFilePath);

        // Act
        var settings = store.LoadSettings();

        // Assert
        settings.Should().NotBeNull();
        settings.Port.Should().Be(5000);
    }
}
