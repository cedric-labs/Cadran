using Ledger.Features.Security.Context;
using Ledger.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Tests.Fixtures;

public static class TestDbContextFactory
{
    public static (AppDbContext context, SqliteConnection connection) Create(string userId = "test-user-id")
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var mockCurrentUserService = new TestCurrentUserService(userId);
        var context = new AppDbContext(options, mockCurrentUserService);
        context.Database.EnsureCreated();

        return (context, connection);
    }

    private class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(string userId)
        {
            UserId = userId;
        }

        public string? UserId { get; }
        public string? UserEmail => "test@Ledger.local";
        public bool IsAuthenticated => true;
    }
}
