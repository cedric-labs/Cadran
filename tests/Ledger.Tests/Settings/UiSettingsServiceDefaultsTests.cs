using AwesomeAssertions;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ledger.Tests.Settings;

public sealed class UiSettingsServiceDefaultsTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly UiSettingsService _service;

    public UiSettingsServiceDefaultsTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _service = new UiSettingsService(_context, NullLogger<UiSettingsService>.Instance, TimeProvider.System);
    }

    [Fact]
    public async Task EnsureDefaultFontsAndSettingsAsync_WhenNoFontExists_CreatesSevenSystemFontsWithInterActive()
    {
        await _service.EnsureDefaultFontsAndSettingsAsync();

        var fonts = await _context.UiFonts.OrderBy(f => f.DisplayOrder).ToListAsync();
        fonts.Select(f => f.Name).Should().Equal("Inter", "Système (Défaut OS)", "Roboto", "Open Sans", "Poppins", "Plus Jakarta Sans", "Fira Sans");
        fonts.Select(f => f.DisplayOrder).Should().Equal(Enumerable.Range(1, 7));
        fonts.Should().OnlyContain(f => f.IsSystem && f.GoogleFontUrl == null);
        fonts.Where(f => f.IsActive).Select(f => f.Name).Should().Equal("Inter");
        fonts[0].FontFamilyCss.Should().Be("'Inter', system-ui, -apple-system, sans-serif");
        fonts[3].FontFamilyCss.Should().Be("'Open Sans', system-ui, sans-serif");
    }

    [Fact]
    public async Task EnsureDefaultFontsAndSettingsAsync_WhenCalledTwice_DoesNotDuplicateFontsOrSettings()
    {
        await _service.EnsureDefaultFontsAndSettingsAsync();
        await _service.EnsureDefaultFontsAndSettingsAsync();

        (await _context.UiFonts.CountAsync()).Should().Be(7);
        (await _context.AppSettings.CountAsync(s => s.Key == "Ui.FontSize")).Should().Be(1);
        (await _context.AppSettings.CountAsync(s => s.Key == "Ui.DefaultDisplayMode")).Should().Be(1);
    }

    [Fact]
    public async Task EnsureDefaultFontsAndSettingsAsync_WhenSettingsAreMissing_CreatesFontSizeAndDisplayModeDefaults()
    {
        await _service.EnsureDefaultFontsAndSettingsAsync();

        var settings = await _context.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value, StringComparer.Ordinal);
        settings["Ui.FontSize"].Should().Be("14px");
        settings["Ui.DefaultDisplayMode"].Should().Be("compact");
    }

    [Fact]
    public async Task EnsureDefaultFontsAndSettingsAsync_WhenFontsAndSettingsExist_KeepsThemUntouched()
    {
        _context.UiFonts.Add(new UiFont { Name = "Perso", FontFamilyCss = "'Perso'", IsActive = true, IsSystem = false, DisplayOrder = 1 });
        _context.AppSettings.AddRange(
            new AppSettingEntity { Key = "Ui.FontSize", Value = "18px" },
            new AppSettingEntity { Key = "Ui.DefaultDisplayMode", Value = "comfort" });
        await _context.SaveChangesAsync();

        await _service.EnsureDefaultFontsAndSettingsAsync();

        (await _context.UiFonts.Select(f => f.Name).ToListAsync()).Should().Equal("Perso");
        var settings = await _context.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value, StringComparer.Ordinal);
        settings["Ui.FontSize"].Should().Be("18px");
        settings["Ui.DefaultDisplayMode"].Should().Be("comfort");
    }

    [Fact]
    public async Task EnsureDefaultFontsAndSettingsAsync_WhenSystemFontHasRemoteUrl_ClearsOnlySystemFontUrls()
    {
        _context.UiFonts.AddRange(
            new UiFont { Name = "Système", FontFamilyCss = "x", GoogleFontUrl = "https://fonts.example/a", IsActive = false, IsSystem = true, DisplayOrder = 1 },
            new UiFont { Name = "Perso", FontFamilyCss = "y", GoogleFontUrl = "https://fonts.example/b", IsActive = false, IsSystem = false, DisplayOrder = 2 });
        await _context.SaveChangesAsync();

        await _service.EnsureDefaultFontsAndSettingsAsync();

        var urls = await _context.UiFonts.ToDictionaryAsync(f => f.Name, f => f.GoogleFontUrl, StringComparer.Ordinal);
        urls["Système"].Should().BeNull();
        urls["Perso"].Should().Be("https://fonts.example/b");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
