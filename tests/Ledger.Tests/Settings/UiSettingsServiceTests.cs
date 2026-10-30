using AwesomeAssertions;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ledger.Tests.Settings;

public sealed class UiSettingsServiceTests : IDisposable
{
    private readonly Ledger.Infrastructure.Data.AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly UiSettingsService _service;

    public UiSettingsServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _service = new UiSettingsService(_context, NullLogger<UiSettingsService>.Instance);
    }

    [Fact]
    public async Task GetSalaryMonthStartDayAsync_Default_ReturnsOne()
    {
        // Act
        var day = await _service.GetSalaryMonthStartDayAsync();

        // Assert
        day.Should().Be(1);
    }

    [Fact]
    public async Task SetSalaryMonthStartDayAsync_ValidDay_PersistsCorrectly()
    {
        // Act
        await _service.SetSalaryMonthStartDayAsync(25);
        var day = await _service.GetSalaryMonthStartDayAsync();

        // Assert
        day.Should().Be(25);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    [InlineData(29, 28)]
    [InlineData(31, 28)]
    public async Task SetSalaryMonthStartDayAsync_OutOufBounds_ClampsBetween1And28(int inputDay, int expectedDay)
    {
        // Act
        await _service.SetSalaryMonthStartDayAsync(inputDay);
        var day = await _service.GetSalaryMonthStartDayAsync();

        // Assert
        day.Should().Be(expectedDay);
    }

    [Fact]
    public void GetSalaryMonthBounds_StartDay25_CalculatesAccurately()
    {
        // Arrange
        var refDate = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var (start, end) = _service.GetSalaryMonthBounds(refDate, 25);

        // Assert
        start.Should().Be(new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task SetFontSizeAsync_WhenValid_PersistsAndCanBeRetrieved()
    {
        await _service.SetFontSizeAsync("16px");
        var size = await _service.GetFontSizeAsync();
        size.Should().Be("16px");

        // Invalid fallback
        await _service.SetFontSizeAsync("99px");
        var fallback = await _service.GetFontSizeAsync();
        fallback.Should().Be("14px");
    }

    [Fact]
    public async Task SetDefaultDisplayModeAsync_WhenValid_PersistsAndCanBeRetrieved()
    {
        await _service.SetDefaultDisplayModeAsync("comfort");
        var mode = await _service.GetDefaultDisplayModeAsync();
        mode.Should().Be("comfort");

        // Invalid fallback
        await _service.SetDefaultDisplayModeAsync("invalid");
        var fallback = await _service.GetDefaultDisplayModeAsync();
        fallback.Should().Be("compact");
    }

    [Fact]
    public async Task SetDefaultPeriodAsync_WhenValid_PersistsAndCanBeRetrieved()
    {
        await _service.SetDefaultPeriodAsync("last-month");
        var period = await _service.GetDefaultPeriodAsync();
        period.Should().Be("last-month");

        // Invalid fallback
        await _service.SetDefaultPeriodAsync("century");
        var fallback = await _service.GetDefaultPeriodAsync();
        fallback.Should().Be("current-month");
    }

    [Fact]
    public async Task SetTransactionsPageSizeAsync_WhenValid_PersistsAndCanBeRetrieved()
    {
        await _service.SetTransactionsPageSizeAsync(50);
        var size = await _service.GetTransactionsPageSizeAsync();
        size.Should().Be(50);

        // Fallback for pageSize <= 0
        await _service.SetTransactionsPageSizeAsync(-5);
        var fallback = await _service.GetTransactionsPageSizeAsync();
        fallback.Should().Be(250);
    }

    [Fact]
    public async Task SetShowKpiCentsAsync_WhenValid_PersistsAndCanBeRetrieved()
    {
        await _service.SetShowKpiCentsAsync(true);
        var show = await _service.GetShowKpiCentsAsync();
        show.Should().BeTrue();

        await _service.SetShowKpiCentsAsync(false);
        var hide = await _service.GetShowKpiCentsAsync();
        hide.Should().BeFalse();
    }

    [Fact]
    public async Task SetDuplicateToleranceDaysAsync_WhenValid_PersistsAndCanBeRetrieved()
    {
        await _service.SetDuplicateToleranceDaysAsync(5);
        var days = await _service.GetDuplicateToleranceDaysAsync();
        days.Should().Be(5);

        // Out of bounds clamping (0..30)
        await _service.SetDuplicateToleranceDaysAsync(-1);
        (await _service.GetDuplicateToleranceDaysAsync()).Should().Be(0);

        await _service.SetDuplicateToleranceDaysAsync(45);
        (await _service.GetDuplicateToleranceDaysAsync()).Should().Be(30);
    }

    [Fact]
    public async Task SetTableColumnsConfigAsync_WhenValid_PersistsAndCanBeRetrieved()
    {
        const string json = "{\"Date\":true,\"Amount\":true}";
        await _service.SetTableColumnsConfigAsync("tx_table", json);

        var retrieved = await _service.GetTableColumnsConfigAsync("tx_table");
        retrieved.Should().Be(json);
    }

    [Fact]
    public async Task GetActiveFontAsync_WhenEmpty_ShouldReturnFallbackFont()
    {
        var font = await _service.GetActiveFontAsync();
        font.Should().NotBeNull();
        font.Name.Should().Be("Inter");
    }

    [Fact]
    public async Task EnsureDefaultSettingsAsync_WhenObsoleteRemoteFontsExist_CleansUpGoogleFontUrl()
    {
        _context.UiFonts.Add(new UiFont
        {
            Name = "Inter",
            FontFamilyCss = "'Inter'",
            GoogleFontUrl = "https://fonts.googleapis.com/css2?family=Inter",
            IsActive = true,
            IsSystem = true,
            DisplayOrder = 1
        });
        await _context.SaveChangesAsync();

        await _service.EnsureDefaultFontsAndSettingsAsync();

        var font = await _context.UiFonts.FirstAsync(f => f.Name == "Inter");
        font.GoogleFontUrl.Should().BeNull();
    }

    [Fact]
    public Task SetTableColumnsConfigAsync_OnError_ThrowsInvalidOperationException()
    {
        _context.Dispose();
        var act = async () => await _service.SetTableColumnsConfigAsync("tx", "{}");
        return act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public Task GetActiveFontAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var act = async () => await _service.GetActiveFontAsync(new CancellationToken(canceled: true));
        return act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public Task GetShowKpiCentsAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var act = async () => await _service.GetShowKpiCentsAsync(new CancellationToken(canceled: true));
        return act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public Task GetTableColumnsConfigAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var act = async () => await _service.GetTableColumnsConfigAsync("tx", new CancellationToken(canceled: true));
        return act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public Task SetTableColumnsConfigAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var act = async () => await _service.SetTableColumnsConfigAsync("tx", "{}", new CancellationToken(canceled: true));
        return act.Should().ThrowAsync<OperationCanceledException>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
