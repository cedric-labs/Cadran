using AwesomeAssertions;
using Ledger.Features.Categories.Entities;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Ledger.Tests.Infrastructure.Data;

public sealed class AppDbContextTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;

    public AppDbContextTests()
    {
        (_context, _connection) = TestDbContextFactory.Create(timeProvider: _timeProvider);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityIsModified_SetsUpdatedAtUtcFromTimeProvider()
    {
        var category = new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Loisirs" };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        var fixedUtc = new DateTimeOffset(2025, 3, 15, 10, 30, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(fixedUtc);

        category.Name = "Sorties";
        await _context.SaveChangesAsync();

        category.UpdatedAtUtc.Should().Be(fixedUtc.UtcDateTime);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
