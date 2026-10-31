using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Entities;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Tests.Accounts;

public sealed class ManualEntryMergerTests : IDisposable
{
    private static readonly DateTime Day = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly ManualEntryMerger _merger;
    private readonly Guid _accountId = Guid.NewGuid();

    public ManualEntryMergerTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque Test" };
        _context.Banks.Add(bank);
        _context.BankAccounts.Add(new BankAccount { Id = _accountId, UserId = "test-user-id", BankId = bank.Id, Name = "Compte Courant" });
        _context.SaveChanges();

        _merger = new ManualEntryMerger(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Transaction Add(TransactionOrigin origin, Action<Transaction>? configure = null)
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
        configure?.Invoke(transaction);
        _context.Transactions.Add(transaction);
        _context.SaveChanges();
        return transaction;
    }

    private Category AddCategory(string name)
    {
        var category = new Category { UserId = "test-user-id", Name = name };
        _context.Categories.Add(category);
        _context.SaveChanges();
        return category;
    }

    [Fact]
    public async Task MergeAsync_WhenPairIsValid_DeletesTheManualTransactionAndKeepsTheImportedOne()
    {
        var manual = Add(TransactionOrigin.Manual);
        var imported = Add(TransactionOrigin.Import);

        var result = await _merger.MergeAsync(manual.Id, imported.Id);

        result.Should().Be(ManualEntryMergeResult.Merged);
        _context.Transactions.AsNoTracking().Select(t => t.Id).Should().BeEquivalentTo([imported.Id]);
    }

    [Fact]
    public async Task MergeAsync_WhenImportedFieldsAreEmpty_CopiesThemFromTheManualTransaction()
    {
        var category = AddCategory("Alimentation");
        var manual = Add(TransactionOrigin.Manual, t =>
        {
            t.CategoryId = category.Id;
            t.Notes = "pour la fête";
            t.BudgetDate = Day.AddMonths(1);
        });
        var imported = Add(TransactionOrigin.Import);

        await _merger.MergeAsync(manual.Id, imported.Id);

        var saved = await _context.Transactions.AsNoTracking().SingleAsync();
        saved.CategoryId.Should().Be(category.Id);
        saved.Notes.Should().Be("pour la fête");
        saved.BudgetDate.Should().Be(Day.AddMonths(1));
    }

    [Fact]
    public async Task MergeAsync_WhenImportedFieldsAreFilled_KeepsTheImportedValues()
    {
        var manualCategory = AddCategory("Loisirs");
        var importedCategory = AddCategory("Alimentation");
        var manual = Add(TransactionOrigin.Manual, t =>
        {
            t.CategoryId = manualCategory.Id;
            t.Notes = "saisie";
        });
        var imported = Add(TransactionOrigin.Import, t =>
        {
            t.CategoryId = importedCategory.Id;
            t.Notes = "banque";
            t.Status = TransactionStatus.Reconciled;
        });

        await _merger.MergeAsync(manual.Id, imported.Id);

        var saved = await _context.Transactions.AsNoTracking().SingleAsync();
        saved.CategoryId.Should().Be(importedCategory.Id);
        saved.Notes.Should().Be("banque");
        saved.Label.Should().Be("CB CARREFOUR");
        saved.Status.Should().Be(TransactionStatus.Reconciled);
        saved.Origin.Should().Be(TransactionOrigin.Import);
    }

    [Fact]
    public async Task MergeAsync_WhenImportedNotesAreBlank_CopiesTheManualNotes()
    {
        var manual = Add(TransactionOrigin.Manual, t => t.Notes = "à garder");
        var imported = Add(TransactionOrigin.Import, t => t.Notes = "  ");

        await _merger.MergeAsync(manual.Id, imported.Id);

        (await _context.Transactions.AsNoTracking().SingleAsync()).Notes.Should().Be("à garder");
    }

    [Fact]
    public async Task MergeAsync_WhenOnlyTheManualTransactionIsSplit_MovesTheSplitsToTheImportedOne()
    {
        var manual = Add(TransactionOrigin.Manual, t => t.IsSplit = true);
        _context.TransactionSplits.AddRange(
            new TransactionSplit { UserId = "test-user-id", TransactionId = manual.Id, Amount = 10m },
            new TransactionSplit { UserId = "test-user-id", TransactionId = manual.Id, Amount = 15m });
        await _context.SaveChangesAsync();
        var imported = Add(TransactionOrigin.Import);

        await _merger.MergeAsync(manual.Id, imported.Id);

        var saved = await _context.Transactions.AsNoTracking().Include(t => t.Splits).SingleAsync();
        saved.Id.Should().Be(imported.Id);
        saved.IsSplit.Should().BeTrue();
        saved.Splits.Select(s => s.Amount).Should().BeEquivalentTo([10m, 15m]);
    }

    [Fact]
    public async Task MergeAsync_WhenImportedTransactionIsAlreadySplit_KeepsItsSplitsAndDropsTheManualOnes()
    {
        var manual = Add(TransactionOrigin.Manual, t => t.IsSplit = true);
        _context.TransactionSplits.Add(new TransactionSplit { UserId = "test-user-id", TransactionId = manual.Id, Amount = 25m });
        var imported = Add(TransactionOrigin.Import, t => t.IsSplit = true);
        _context.TransactionSplits.AddRange(
            new TransactionSplit { UserId = "test-user-id", TransactionId = imported.Id, Amount = 5m },
            new TransactionSplit { UserId = "test-user-id", TransactionId = imported.Id, Amount = 20m });
        await _context.SaveChangesAsync();

        await _merger.MergeAsync(manual.Id, imported.Id);

        _context.TransactionSplits.AsNoTracking().Select(s => s.Amount).Should().BeEquivalentTo([5m, 20m]);
    }

    [Fact]
    public async Task MergeAsync_WhenATransactionDoesNotExist_ReturnsNotFoundAndChangesNothing()
    {
        var manual = Add(TransactionOrigin.Manual);

        var result = await _merger.MergeAsync(manual.Id, Guid.NewGuid());

        result.Should().Be(ManualEntryMergeResult.NotFound);
        _context.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task MergeAsync_WhenFirstTransactionIsNotManual_ReturnsNotManualAndChangesNothing()
    {
        var first = Add(TransactionOrigin.Import);
        var second = Add(TransactionOrigin.Import);

        var result = await _merger.MergeAsync(first.Id, second.Id);

        result.Should().Be(ManualEntryMergeResult.NotManual);
        _context.Transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task MergeAsync_WhenSecondTransactionIsNotImported_ReturnsNotImportedAndChangesNothing()
    {
        var first = Add(TransactionOrigin.Manual);
        var second = Add(TransactionOrigin.Manual);

        var result = await _merger.MergeAsync(first.Id, second.Id);

        result.Should().Be(ManualEntryMergeResult.NotImported);
        _context.Transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task MergeAsync_WhenAmountsDiffer_ReturnsIncompatibleAndChangesNothing()
    {
        var manual = Add(TransactionOrigin.Manual);
        var imported = Add(TransactionOrigin.Import, t => t.Amount = 26m);

        var result = await _merger.MergeAsync(manual.Id, imported.Id);

        result.Should().Be(ManualEntryMergeResult.Incompatible);
        _context.Transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task MergeAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        var manual = Add(TransactionOrigin.Manual);
        var imported = Add(TransactionOrigin.Import);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _merger.MergeAsync(manual.Id, imported.Id, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
