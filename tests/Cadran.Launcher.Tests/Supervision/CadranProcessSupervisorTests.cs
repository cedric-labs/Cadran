using AwesomeAssertions;
using Cadran.Launcher.Supervision;

namespace Cadran.Launcher.Tests.Supervision;

public sealed class CadranProcessSupervisorTests
{
    [Fact]
    public void Constructor_WhenStateProvided_ExposesCorrectState()
    {
        var args = new ProcessStateChangedEventArgs(ProcessState.Running);
        args.State.Should().Be(ProcessState.Running);
    }

    [Fact]
    public void Constructor_WhenLogEntryProvided_ExposesCorrectLogEntry()
    {
        var entry = new LogEntry(DateTimeOffset.UtcNow, LogLevel.Warning, "Warning message");
        var args = new LogReceivedEventArgs(entry);
        args.Entry.Should().Be(entry);
    }

    [Fact]
    public async Task Constructor_WhenParametersProvided_InitializesWithStoppedStateAndProperUrl()
    {
        await using var supervisor = new CadranProcessSupervisor("C:\\dummy\\Cadran.exe", "C:\\dummy", 5050);

        supervisor.State.Should().Be(ProcessState.Stopped);
        supervisor.ProcessId.Should().BeNull();
        supervisor.ServerUrl.Should().Be("http://localhost:5050");
    }

    [Fact]
    public async Task StartAsync_WhenExecutableIsMissing_ReturnsFalseAndLogsError()
    {
        await using var supervisor = new CadranProcessSupervisor("C:\\non_existent_folder\\Cadran.exe", "C:\\non_existent", 5050);
        var loggedErrors = new List<LogEntry>();
        supervisor.LogReceived += (_, e) =>
        {
            if (e.Entry.Level == LogLevel.Error)
            {
                loggedErrors.Add(e.Entry);
            }
        };

        var result = await supervisor.StartAsync();

        result.Should().BeFalse();
        supervisor.State.Should().Be(ProcessState.Stopped);
        loggedErrors.Should().NotBeEmpty();
        loggedErrors[0].Message.Should().Contain("introuvable");
    }

    [Fact]
    public async Task StopAsync_WhenProcessIsNotRunning_ReturnsTrue()
    {
        await using var supervisor = new CadranProcessSupervisor("C:\\dummy\\Cadran.exe", "C:\\dummy", 5050);

        var result = await supervisor.StopAsync();

        result.Should().BeTrue();
        supervisor.State.Should().Be(ProcessState.Stopped);
    }
}
