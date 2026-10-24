using Cadran.Launcher.Backups;
using FluentAssertions;

namespace Cadran.Launcher.Tests.Backups;

public sealed class BackupAndRestoreResultTests
{
    [Fact]
    public void BackupResult_Success_CreatesValidSuccessState()
    {
        var result = BackupResult.Success("C:\\backup.db");
        result.IsSuccess.Should().BeTrue();
        result.BackupFilePath.Should().Be("C:\\backup.db");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void BackupResult_Failure_CreatesValidFailureState()
    {
        var result = BackupResult.Failure("Disk full");
        result.IsSuccess.Should().BeFalse();
        result.BackupFilePath.Should().BeNull();
        result.ErrorMessage.Should().Be("Disk full");
    }

    [Fact]
    public void RestoreResult_Success_CreatesValidSuccessState()
    {
        var result = RestoreResult.Success();
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void RestoreResult_Failure_CreatesValidFailureState()
    {
        var result = RestoreResult.Failure("Corrupted file");
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Corrupted file");
    }
}
