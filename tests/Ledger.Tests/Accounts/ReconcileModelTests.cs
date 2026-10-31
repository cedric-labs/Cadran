using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Pages;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Ledger.Tests.Accounts;

public sealed class ReconcileModelTests : IDisposable
{
    private static readonly DateTime Day = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly ReconcileModel _model;
    private readonly Guid _accountId = Guid.NewGuid();

    public ReconcileModelTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque Test" };
        _context.Banks.Add(bank);
        _context.BankAccounts.Add(new BankAccount { Id = _accountId, UserId = "test-user-id", BankId = bank.Id, Name = "Compte Courant" });
        _context.SaveChanges();

        var settings = new Mock<IUiSettingsService>();
        settings.Setup(s => s.GetDuplicateToleranceDaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var httpContext = new DefaultHttpContext();
        _model = new ReconcileModel(new ManualEntryMatchLoader(_context, settings.Object), new ManualEntryMerger(_context))
        {
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Transaction Add(TransactionOrigin origin)
    {
        var transaction = new Transaction
        {
            UserId = "test-user-id",
            BankAccountId = _accountId,
            Date = Day,
            Amount = 25m,
            Type = TransactionType.Debit,
            Label = origin == TransactionOrigin.Manual ? "Courses" : "CB CARREFOUR",
            Origin = origin,
        };
        _context.Transactions.Add(transaction);
        _context.SaveChanges();
        return transaction;
    }

    [Fact]
    public async Task OnGetAsync_WhenAPairExists_ExposesIt()
    {
        Add(TransactionOrigin.Manual);
        Add(TransactionOrigin.Import);

        await _model.OnGetAsync();

        _model.Matches.Should().ContainSingle();
    }

    [Fact]
    public async Task OnPostMergeAsync_WhenPairIsValid_MergesAndReportsMerged()
    {
        var manual = Add(TransactionOrigin.Manual);
        var imported = Add(TransactionOrigin.Import);

        var result = await _model.OnPostMergeAsync(manual.Id, imported.Id);

        result.Should().BeOfType<RedirectToPageResult>();
        _model.TempData["ReconcileResult"].Should().Be("Merged");
        (await _context.Transactions.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task OnPostMergeAsync_WhenManualTransactionIsUnknown_ReportsNotFoundAndKeepsData()
    {
        var imported = Add(TransactionOrigin.Import);

        await _model.OnPostMergeAsync(Guid.NewGuid(), imported.Id);

        _model.TempData["ReconcileResult"].Should().Be("NotFound");
        (await _context.Transactions.AsNoTracking().CountAsync()).Should().Be(1);
    }
}
