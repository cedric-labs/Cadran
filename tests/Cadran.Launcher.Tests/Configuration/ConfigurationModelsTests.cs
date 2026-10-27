using AwesomeAssertions;
using Cadran.Launcher.Configuration;

namespace Cadran.Launcher.Tests.Configuration;

public sealed class ConfigurationModelsTests
{
    [Fact]
    public void Normalize_WhenRetentionValuesAreOutOfBounds_ClampsCorrectly()
    {
        // Arrange & Act
        var tooLow = new BackupRetentionOptions(BackupRetentionMode.FileCount, MaxBackupFiles: -5, MaxBackupDays: 0).Normalize();
        var tooHigh = new BackupRetentionOptions(BackupRetentionMode.Both, MaxBackupFiles: 1000, MaxBackupDays: 50000).Normalize();

        // Assert
        tooLow.MaxBackupFiles.Should().Be(1);
        tooLow.MaxBackupDays.Should().Be(1);

        tooHigh.MaxBackupFiles.Should().Be(500);
        tooHigh.MaxBackupDays.Should().Be(3650);
    }

    [Fact]
    public void Normalize_WhenPortIsInvalid_ResetsTo5000()
    {
        // Arrange & Act
        var invalidPortLow = new LauncherSettings(Port: 80).Normalize();
        var invalidPortHigh = new LauncherSettings(Port: 70000).Normalize();
        var validPort = new LauncherSettings(Port: 8080).Normalize();

        // Assert
        invalidPortLow.Port.Should().Be(5000);
        invalidPortHigh.Port.Should().Be(5000);
        validPort.Port.Should().Be(8080);
    }

    [Fact]
    public void Normalize_WhenLanguageIsSpecified_NormalizesToSupportedLanguageOrFallbackToFr()
    {
        // Arrange & Act
        var english = new LauncherSettings(Language: "en-US").Normalize();
        var french = new LauncherSettings(Language: "fr-FR").Normalize();
        var spanish = new LauncherSettings(Language: "es-ES").Normalize();
        var german = new LauncherSettings(Language: "de-DE").Normalize();
        var unknown = new LauncherSettings(Language: "it").Normalize();

        // Assert
        english.Language.Should().Be("en");
        french.Language.Should().Be("fr");
        spanish.Language.Should().Be("es");
        german.Language.Should().Be("de");
        unknown.Language.Should().Be("fr");
    }
}
