using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Ledger.Tests.Configuration;

public class LocalizedEntityServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly LocalizedEntityService _service;

    public LocalizedEntityServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _service = new LocalizedEntityService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetTranslationAsync_ReturnsFallback_WhenNoTranslationExists()
    {
        var id = Guid.NewGuid();
        var result = await _service.GetTranslationAsync("Category", id, "Name", "Alimentation", "en");

        Assert.Equal("Alimentation", result);
    }

    [Fact]
    public async Task SetTranslationAsync_WhenStored_StoresAndRetrievesCorrectly()
    {
        var id = Guid.NewGuid();

        await _service.SetTranslationAsync("Category", id, "Name", "en", "Food & Dining");

        var enResult = await _service.GetTranslationAsync("Category", id, "Name", "Alimentation", "en");
        var frResult = await _service.GetTranslationAsync("Category", id, "Name", "Alimentation", "fr");

        Assert.Equal("Food & Dining", enResult);
        Assert.Equal("Alimentation", frResult);
    }

    [Fact]
    public async Task SetTranslationAsync_WhenTranslationExists_UpdatesExistingTranslation()
    {
        var id = Guid.NewGuid();

        await _service.SetTranslationAsync("Category", id, "Name", "en", "Groceries");
        await _service.SetTranslationAsync("Category", id, "Name", "en", "Food & Groceries");

        var result = await _service.GetTranslationAsync("Category", id, "Name", "Fallback", "en");
        Assert.Equal("Food & Groceries", result);
    }

    [Fact]
    public async Task SetTranslationAsync_RemovesTranslation_WhenEmptyOrWhitespace()
    {
        var id = Guid.NewGuid();

        await _service.SetTranslationAsync("Category", id, "Name", "en", "Temp");
        await _service.SetTranslationAsync("Category", id, "Name", "en", "   ");

        var result = await _service.GetTranslationAsync("Category", id, "Name", "Fallback", "en");
        Assert.Equal("Fallback", result);
    }

    [Fact]
    public async Task GetTranslationsAsync_WhenMultipleEntities_ReturnsDictionary()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        await _service.SetTranslationAsync("Category", id1, "Name", "en", "Salary");
        await _service.SetTranslationAsync("Category", id2, "Name", "en", "Rent");

        var dict = await _service.GetTranslationsAsync("Category", new[] { id1, id2, id3 }, "Name", "en");

        Assert.Equal(2, dict.Count);
        Assert.Equal("Salary", dict[id1]);
        Assert.Equal("Rent", dict[id2]);
        Assert.False(dict.ContainsKey(id3));
    }

    [Fact]
    public async Task GetAllTranslationsForEntityAsync_WhenInvoked_ReturnsAllLanguagesForGivenEntity()
    {
        var id = Guid.NewGuid();

        await _service.SetTranslationAsync("Category", id, "Name", "en", "Savings");
        await _service.SetTranslationAsync("Category", id, "Name", "es", "Ahorros");
        await _service.SetTranslationAsync("Category", id, "Name", "de", "Ersparnisse");

        var all = await _service.GetAllTranslationsForEntityAsync("Category", id, "Name");

        Assert.Equal(3, all.Count);
        Assert.Equal("Savings", all["en"]);
        Assert.Equal("Ahorros", all["es"]);
        Assert.Equal("Ersparnisse", all["de"]);
    }

    [Fact]
    public async Task GetTranslationAsync_UsesCurrentUICulture_WhenLanguageCodeIsNull()
    {
        var id = Guid.NewGuid();
        var currentLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

        await _service.SetTranslationAsync("Category", id, "Name", currentLang, "Traduction Locale");

        var result = await _service.GetTranslationAsync("Category", id, "Name", "Fallback", null);
        Assert.Equal("Traduction Locale", result);
    }
}
