using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Models;
using Ledger.Features.Recurring.Services;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Ledger.Tests.Recurring;

public sealed class RecurringMonthlySummaryTests : IDisposable
{
    private const string UserId = "test-user-id";

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly RecurringExpenseService _service;
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _otherAccountId = Guid.NewGuid();

    public RecurringMonthlySummaryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _timeProvider.SetLocalTimeZone(TimeZoneInfo.Utc);
        _service = new RecurringExpenseService(_context, _timeProvider);

        var bank = new Bank { Id = Guid.NewGuid(), UserId = UserId, Name = "Banque" };
        _context.Banks.Add(bank);
        _context.BankAccounts.AddRange(
            new BankAccount { Id = _accountId, UserId = UserId, BankId = bank.Id, Name = "Courant" },
            new BankAccount { Id = _otherAccountId, UserId = UserId, BankId = bank.Id, Name = "Autre" });
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_WhenSeveralTransactionsMatch_PicksClosestAmountAndNeverReusesIt()
    {
        AddExpense("Spotify", "SPOTIFY", 10m, day: 5);
        AddExpense("Spotify bis", "SPOTIFY", 10m, day: 6);
        var far = AddDebit("SPOTIFY PREMIUM", 10.4m, new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc));
        var near = AddDebit("SPOTIFY FAMILY", 10m, new DateTime(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc));
        await _context.SaveChangesAsync();

        var summary = await _service.GetMonthlySummaryAsync(2026, 3);

        summary.Items.Should().HaveCount(2);
        summary.Items[0].Expense.Name.Should().Be("Spotify");
        summary.Items[0].ActualTransactionId.Should().Be(near.Id);
        summary.Items[0].Status.Should().Be(RecurringMonthStatus.Debited);
        summary.Items[1].ActualTransactionId.Should().Be(far.Id);
        summary.TotalDebited.Should().Be(20.4m);
        summary.TotalExpected.Should().Be(20m);
        summary.TotalPending.Should().Be(0m);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_WhenCurrentMonthDayIsMoreThanThreeDaysPast_MarksOverdueElsePending()
    {
        AddExpense("Loyer", "LOYER", 800m, day: 10);
        AddExpense("Assurance", "ASSURANCE", 50m, day: 17);
        AddExpense("Gym", "GYM", 30m, day: 25);
        AddExpense("Sans jour", "SANSJOUR", 5m, day: null);
        await _context.SaveChangesAsync();

        var summary = await _service.GetMonthlySummaryAsync(2026, 3);

        var statuses = summary.Items.ToDictionary(i => i.Expense.Name, i => i.Status, StringComparer.Ordinal);
        statuses["Loyer"].Should().Be(RecurringMonthStatus.Overdue);
        statuses["Assurance"].Should().Be(RecurringMonthStatus.Pending);
        statuses["Gym"].Should().Be(RecurringMonthStatus.Pending);
        statuses["Sans jour"].Should().Be(RecurringMonthStatus.Pending);
        summary.TotalPending.Should().Be(885m);
        summary.TotalDebited.Should().Be(0m);
        summary.ProgressPercentage.Should().Be(0);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_WhenMonthIsFutureOrPast_UsesPendingForFutureAndOverdueForPast()
    {
        AddExpense("Loyer", "LOYER", 800m, day: 1);
        await _context.SaveChangesAsync();

        var future = await _service.GetMonthlySummaryAsync(2026, 5);
        var past = await _service.GetMonthlySummaryAsync(2026, 1);

        future.Items.Single().Status.Should().Be(RecurringMonthStatus.Pending);
        past.Items.Single().Status.Should().Be(RecurringMonthStatus.Overdue);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_WhenExpenseTargetsAnotherAccount_IgnoresTransactionsOfOtherAccounts()
    {
        var expense = AddExpense("Loyer", "LOYER", 800m, day: 10);
        expense.TargetBankAccountId = _otherAccountId;
        AddDebit("LOYER MARS", 800m, new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc));
        await _context.SaveChangesAsync();

        var summary = await _service.GetMonthlySummaryAsync(2026, 3);

        summary.Items.Single().Status.Should().Be(RecurringMonthStatus.Overdue);
        summary.Items.Single().ActualTransactionId.Should().BeNull();
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_WhenExpensesAreInactiveOrNotDueThisMonth_SkipsThem()
    {
        AddExpense("Inactive", "INACTIVE", 10m, day: 3).IsActive = false;
        AddExpense("Trimestrielle", "TRIM", 90m, day: 3).Frequency = RecurringFrequency.Quarterly;
        await _context.SaveChangesAsync();

        var summary = await _service.GetMonthlySummaryAsync(2026, 2);

        summary.Items.Should().BeEmpty();
        summary.TotalExpected.Should().Be(0m);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_WhenCreditsExist_SplitsTotalAndSalaryIncome()
    {
        var salaryCategory = new Ledger.Features.Categories.Entities.Category { Id = Guid.NewGuid(), UserId = UserId, Name = "Salaire", IsSalary = true };
        _context.Categories.Add(salaryCategory);
        AddCredit("PAIE", 2000m, salaryCategory.Id);
        AddCredit("REMBOURSEMENT", 100m, null);
        await _context.SaveChangesAsync();

        var summary = await _service.GetMonthlySummaryAsync(2026, 3);

        summary.TotalIncome.Should().Be(2100m);
        summary.SalaryIncome.Should().Be(2000m);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private RecurringExpense AddExpense(string name, string pattern, decimal amount, int? day)
    {
        var expense = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Name = name,
            Pattern = pattern,
            ExpectedAmount = amount,
            AmountTolerancePercentage = 0.05m,
            ExpectedDayOfMonth = day,
            IsActive = true
        };
        _context.RecurringExpenses.Add(expense);
        return expense;
    }

    private Transaction AddDebit(string label, decimal amount, DateTime date)
    {
        var transaction = new Transaction { Id = Guid.NewGuid(), UserId = UserId, BankAccountId = _accountId, Label = label, Amount = amount, Type = TransactionType.Debit, Date = date };
        _context.Transactions.Add(transaction);
        return transaction;
    }

    private void AddCredit(string label, decimal amount, Guid? categoryId)
    {
        _context.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            BankAccountId = _accountId,
            Label = label,
            Amount = amount,
            Type = TransactionType.Credit,
            Date = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
            CategoryId = categoryId
        });
    }
}
