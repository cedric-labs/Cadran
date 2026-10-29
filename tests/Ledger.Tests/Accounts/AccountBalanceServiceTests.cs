using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;

namespace Ledger.Tests.Accounts;

public sealed class AccountBalanceServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly AccountBalanceService _service;
    private readonly Bank _bank;

    public AccountBalanceServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        _bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "BNP Paribas" };
        _context.Banks.Add(_bank);
        _context.SaveChanges();

        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();
        _mockLocalizedEntityService
            .Setup(l => l.GetTranslationAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Guid _, string _, string defaultVal, string? _, CancellationToken _) => defaultVal);
        _mockLocalizedEntityService
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        _service = new AccountBalanceService(_context, _mockLocalizedEntityService.Object);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public Task GetAccountBalanceAsync_WhenAccountNotFound_ShouldThrowKeyNotFoundException()
    {
        var act = async () => await _service.GetAccountBalanceAsync(Guid.NewGuid());

        return act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetAccountBalanceAsync_WhenInvoked_CalculatesBalancesCorrectly()
    {
        var customType = new CustomAccountType
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Compte Courant",
            Code = "Checking",
            CategoryGroup = AccountCategoryGroup.Liquid
        };
        _context.CustomAccountTypes.Add(customType);

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Compte Courant Principal",
            Type = AccountType.Checking,
            CustomAccountTypeId = customType.Id,
            InitialBalance = 1000m,
            Currency = "EUR"
        };
        _context.BankAccounts.Add(account);

        _context.Transactions.AddRange(
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = DateTime.Today.AddDays(-2),
                Amount = 500m,
                Type = TransactionType.Credit,
                Status = TransactionStatus.Cleared,
                Label = "Salaire"
            },
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = DateTime.Today.AddDays(-1),
                Amount = 200m,
                Type = TransactionType.Debit,
                Status = TransactionStatus.Cleared,
                Label = "Loyer"
            },
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = DateTime.Today,
                Amount = 50m,
                Type = TransactionType.Debit,
                Status = TransactionStatus.Pending,
                Label = "Paiement CB en attente"
            }
        );
        await _context.SaveChangesAsync();

        var summary = await _service.GetAccountBalanceAsync(account.Id);

        // Current = Initial (1000) + Credit (500) - Debit (200 + 50) = 1250
        summary.CurrentBalance.Should().Be(1250m);
        // Cleared = Initial (1000) + Credit (500) - Cleared Debit (200) = 1300
        summary.ClearedBalance.Should().Be(1300m);
        // Pending = Current - Cleared = 1250 - 1300 = -50
        summary.PendingBalance.Should().Be(-50m);
        summary.CustomAccountTypeName.Should().Be("Compte Courant");
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WhenInvoked_AggregatesLiquidSavingsAndDebt()
    {
        var checkingAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Compte Courant",
            Type = AccountType.Checking,
            InitialBalance = 2000m,
            IsArchived = false
        };

        var savingsAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Livret A",
            Type = AccountType.Savings,
            InitialBalance = 15000m,
            IsArchived = false
        };

        var overdrawnAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Carte Débit Différé",
            Type = AccountType.CreditCard,
            InitialBalance = -300m,
            IsArchived = false
        };

        var archivedAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Ancien Compte Clôturé",
            Type = AccountType.Checking,
            InitialBalance = 5000m,
            IsArchived = true
        };

        _context.BankAccounts.AddRange(checkingAccount, savingsAccount, overdrawnAccount, archivedAccount);
        await _context.SaveChangesAsync();

        var dashboard = await _service.GetDashboardSummaryAsync();

        // Archived account must be excluded
        dashboard.Accounts.Should().NotContain(a => a.AccountId == archivedAccount.Id);
        dashboard.Accounts.Should().HaveCount(3);

        dashboard.TotalLiquidAssets.Should().Be(2000m);
        dashboard.TotalSavings.Should().Be(15000m);
        dashboard.TotalDebts.Should().Be(300m);
        // NetWorth = TotalLiquid (2000) + TotalSavings (15000) - TotalDebts (300) = 16700
        dashboard.TotalNetWorth.Should().Be(16700m);
    }

    [Fact]
    public Task CalculateBalanceAtDateAsync_WhenNotFound_ShouldThrow()
    {
        var act = async () => await _service.CalculateBalanceAtDateAsync(Guid.NewGuid(), DateTime.Today);

        return act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CalculateBalanceAtDateAsync_WhenTargetDateSpecified_OnlyIncludesTransactionsUpToTargetDate()
    {
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Compte Test Date",
            Type = AccountType.Checking,
            InitialBalance = 100m
        };
        _context.BankAccounts.Add(account);

        var cutoffDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _context.Transactions.AddRange(
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                Amount = 50m,
                Type = TransactionType.Credit,
                Label = "Avant cutoff"
            },
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                Amount = 20m,
                Type = TransactionType.Debit,
                Label = "Jour du cutoff"
            },
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
                Amount = 80m,
                Type = TransactionType.Credit,
                Label = "Après cutoff (ignoré)"
            }
        );
        await _context.SaveChangesAsync();

        var balance = await _service.CalculateBalanceAtDateAsync(account.Id, cutoffDate);

        // Initial (100) + Credit (50) - Debit (20) = 130
        balance.Should().Be(130m);
    }
}
