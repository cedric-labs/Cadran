using Cadran.Launcher.Supervision;
using FluentAssertions;

namespace Cadran.Launcher.Tests.Supervision;

public sealed class LogBufferStreamTests
{
    [Fact]
    public void Append_WhenCapacityExceeded_EvictsOldestEntries()
    {
        // Arrange
        var buffer = new LogBufferStream(capacity: 100);

        // Act
        for (var i = 1; i <= 105; i++)
        {
            buffer.Append(new LogEntry(DateTimeOffset.UtcNow, LogLevel.Information, $"Message {i}"));
        }

        var all = buffer.GetAll();

        // Assert
        all.Should().HaveCount(100);
        all[0].Message.Should().Be("Message 6");
        all[^1].Message.Should().Be("Message 105");
    }

    [Fact]
    public void Filter_WhenKeywordProvided_ReturnsMatchingEntriesOnly()
    {
        // Arrange
        var buffer = new LogBufferStream(capacity: 100);
        buffer.Append(new LogEntry(DateTimeOffset.UtcNow, LogLevel.Information, "Kestrel listening on http://localhost:5000"));
        buffer.Append(new LogEntry(DateTimeOffset.UtcNow, LogLevel.Warning, "Slow query detected on database"));
        buffer.Append(new LogEntry(DateTimeOffset.UtcNow, LogLevel.Error, "Database lock acquired"));

        // Act
        var matches = buffer.Filter("Database");

        // Assert
        matches.Should().HaveCount(2);
        matches.Should().Contain(e => e.Level == LogLevel.Warning);
        matches.Should().Contain(e => e.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData("info: Microsoft.Hosting.Lifetime[14]", LogLevel.Information)]
    [InlineData("warn: Microsoft.AspNetCore.HttpsPolicy[1]", LogLevel.Warning)]
    [InlineData("fail: Microsoft.AspNetCore.Diagnostics[1]", LogLevel.Error)]
    [InlineData("crit: Application crashed", LogLevel.Critical)]
    [InlineData("dbug: Running query", LogLevel.Debug)]
    public void ParseLogLevel_WhenGivenStandardPrefix_ReturnsExpectedSeverity(string line, LogLevel expected)
    {
        // Act
        var level = LogEntry.ParseLogLevel(line);

        // Assert
        level.Should().Be(expected);
    }
}
