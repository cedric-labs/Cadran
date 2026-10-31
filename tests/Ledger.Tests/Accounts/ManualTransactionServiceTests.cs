using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Ledger.Tests.Accounts;

public sealed class ManualTransactionServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly ManualTransactionService _service;
    private readonly Guid _accountId = Guid.NewGuid();

    public ManualTransactionServiceTests()
    {
        var timeProvider = new FakeTimeProvider(Now);
        (_context, _connection) = TestDbContextFactory.Create(timeProvider: timeProvider);

        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque Test" };
        _context.Banks.Add(bank);
        _context.BankAccounts.Add(new BankAccount { Id = _accountId, UserId = "test-user-id", BankId = bank.Id, Name = "Compte Courant" });
        _context.SaveChanges();

        _service = new ManualTransactionService(_context, timeProvider);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private ManualTransactionRequest Request(
        Guid? accountId = null,
        DateTime? date = null,
        string? label = "Boulangerie",
        decimal amount = 12.50m,
        TransactionType type = TransactionType.Debit) =>
        new(accountId ?? _accountId, date ?? new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Unspecified), label, amount, type);

    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_PersistsManualTransaction()
    {
        var result = await _service.CreateAsync(Request());

        result.Succeeded.Should().BeTrue();
        var saved = await _context.Transactions.SingleAsync();
        saved.Origin.Should().Be(TransactionOrigin.Manual);
        saved.Label.Should().Be("Boulangerie");
        saved.Amount.Should().Be(12.50m);
        saved.Type.Should().Be(TransactionType.Debit);
        saved.BankAccountId.Should().Be(_accountId);
        saved.ImportBatchId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenLabelHasSurroundingSpaces_TrimsLabel()
    {
        var result = await _service.CreateAsync(Request(label: "  Café  "));

        result.Transaction.Should().NotBeNull();
        result.Transaction.Label.Should().Be("Café");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WhenLabelIsBlank_ReturnsLabelRequiredAndPersistsNothing(string? label)
    {
        var result = await _service.CreateAsync(Request(label: label));

        result.Error.Should().Be(ManualTransactionError.LabelRequired);
        (await _context.Transactions.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateAsync_WhenAmountIsNotPositive_ReturnsInvalidAmount(double amount)
    {
        var result = await _service.CreateAsync(Request(amount: (decimal)amount));

        result.Error.Should().Be(ManualTransactionError.InvalidAmount);
        (await _context.Transactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_WhenAccountDoesNotExist_ReturnsAccountNotFound()
    {
        var result = await _service.CreateAsync(Request(accountId: Guid.NewGuid()));

        result.Error.Should().Be(ManualTransactionError.AccountNotFound);
        (await _context.Transactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_WhenDateIsDefault_ReturnsInvalidDate()
    {
        var result = await _service.CreateAsync(Request(date: default(DateTime)));

        result.Error.Should().Be(ManualTransactionError.InvalidDate);
    }

    [Fact]
    public async Task CreateAsync_WhenTypeIsTransfer_ReturnsUnsupportedType()
    {
        var result = await _service.CreateAsync(Request(type: TransactionType.Transfer));

        result.Error.Should().Be(ManualTransactionError.UnsupportedType);
    }

    [Fact]
    public async Task CreateAsync_WhenTypeIsCredit_StoresCredit()
    {
        var result = await _service.CreateAsync(Request(type: TransactionType.Credit));

        result.Transaction.Should().NotBeNull();
        result.Transaction.Type.Should().Be(TransactionType.Credit);
    }

    [Fact]
    public async Task CreateAsync_WhenDateIsTodayOrPast_SetsStatusCleared()
    {
        var result = await _service.CreateAsync(Request(date: new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Unspecified)));

        result.Transaction.Should().NotBeNull();
        result.Transaction.Status.Should().Be(TransactionStatus.Cleared);
    }

    [Fact]
    public async Task CreateAsync_WhenDateIsInTheFuture_SetsStatusPending()
    {
        var result = await _service.CreateAsync(Request(date: new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Unspecified)));

        result.Transaction.Should().NotBeNull();
        result.Transaction.Status.Should().Be(TransactionStatus.Pending);
    }

    [Fact]
    public async Task CreateAsync_WhenNotesAreBlank_StoresNullNotes()
    {
        var request = Request() with { Notes = "   " };

        var result = await _service.CreateAsync(request);

        result.Transaction.Should().NotBeNull();
        result.Transaction.Notes.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _service.CreateAsync(Request(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
