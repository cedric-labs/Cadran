using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Moq;

namespace Ledger.Tests.Accounts;

public sealed class ManualEntryMatchLoaderTests : IDisposable
{
    private static readonly DateTime Day = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<IUiSettingsService> _settings = new();
    private readonly ManualEntryMatchLoader _loader;
    private readonly Guid _accountId = Guid.NewGuid();

    public ManualEntryMatchLoaderTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque Test" };
        _context.Banks.Add(bank);
        _context.BankAccounts.Add(new BankAccount { Id = _accountId, UserId = "test-user-id", BankId = bank.Id, Name = "Compte Courant" });
        _context.SaveChanges();

        _settings.Setup(s => s.GetDuplicateToleranceDaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);
        _loader = new ManualEntryMatchLoader(_context, _settings.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Transaction Add(TransactionOrigin origin, DateTime date, decimal amount = 25m)
    {
        var transaction = new Transaction
        {
            UserId = "test-user-id",
            BankAccountId = _accountId,
            Date = date,
            Amount = amount,
            Type = TransactionType.Debit,
            Label = origin == TransactionOrigin.Manual ? "Courses" : "CB CARREFOUR",
            Origin = origin,
        };
        _context.Transactions.Add(transaction);
        _context.SaveChanges();
        return transaction;
    }

    [Fact]
    public async Task LoadAsync_WhenImportedIsWithinConfiguredTolerance_ReturnsThePair()
    {
        var manual = Add(TransactionOrigin.Manual, Day);
        var imported = Add(TransactionOrigin.Import, Day.AddDays(3));

        var matches = await _loader.LoadAsync();

        matches.Should().ContainSingle();
        matches[0].Manual.Id.Should().Be(manual.Id);
        matches[0].Imported.Id.Should().Be(imported.Id);
    }

    [Fact]
    public async Task LoadAsync_WhenImportedIsBeyondConfiguredTolerance_ReturnsNoPair()
    {
        Add(TransactionOrigin.Manual, Day);
        Add(TransactionOrigin.Import, Day.AddDays(4));

        var matches = await _loader.LoadAsync();

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenThereIsNoManualTransaction_ReturnsNoPair()
    {
        Add(TransactionOrigin.Import, Day);

        var matches = await _loader.LoadAsync();

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenPairIsReturned_ExposesAccountNameForDisplay()
    {
        Add(TransactionOrigin.Manual, Day);
        Add(TransactionOrigin.Import, Day);

        var matches = await _loader.LoadAsync();

        matches[0].Manual.BankAccount.Should().NotBeNull();
        matches[0].Manual.BankAccount?.Name.Should().Be("Compte Courant");
    }

    [Fact]
    public async Task LoadAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _loader.LoadAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
