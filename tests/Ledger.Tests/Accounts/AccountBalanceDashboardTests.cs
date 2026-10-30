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

public sealed class AccountBalanceDashboardTests : IDisposable
{
    private const string UserId = "test-user-id";

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<ILocalizedEntityService> _localizedEntityService = new();
    private readonly AccountBalanceService _service;
    private readonly Bank _bank;

    public AccountBalanceDashboardTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _bank = new Bank { Id = Guid.NewGuid(), UserId = UserId, Name = "BNP Paribas" };
        _context.Banks.Add(_bank);
        _context.SaveChanges();

        _localizedEntityService
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _service = new AccountBalanceService(_context, _localizedEntityService.Object);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WhenAccountHasTransactions_ComputesCurrentClearedAndPendingBalances()
    {
        var account = NewAccount("Courant", AccountType.Checking, 1000m);
        account.Transactions.Add(NewTransaction(account, TransactionType.Credit, 500m, TransactionStatus.Cleared));
        account.Transactions.Add(NewTransaction(account, TransactionType.Debit, 200m, TransactionStatus.Cleared));
        account.Transactions.Add(NewTransaction(account, TransactionType.Debit, 50m, TransactionStatus.Pending));
        account.Transactions.Add(NewTransaction(account, TransactionType.Credit, 30m, TransactionStatus.Pending));
        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync();

        var dashboard = await _service.GetDashboardSummaryAsync();

        var summary = dashboard.Accounts.Single();
        summary.CurrentBalance.Should().Be(1280m);
        summary.ClearedBalance.Should().Be(1300m);
        summary.PendingBalance.Should().Be(-20m);
        summary.BankName.Should().Be("BNP Paribas");
        dashboard.TotalLiquidAssets.Should().Be(1280m);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WhenLiquidAccountIsOverdrawn_CountsItAsDebt()
    {
        _context.BankAccounts.AddRange(
            NewAccount("Courant négatif", AccountType.Checking, -120m),
            NewAccount("Carte positive", AccountType.CreditCard, 80m),
            NewAccount("Carte négative", AccountType.CreditCard, -40m));
        await _context.SaveChangesAsync();

        var dashboard = await _service.GetDashboardSummaryAsync();

        dashboard.TotalLiquidAssets.Should().Be(80m);
        dashboard.TotalDebts.Should().Be(160m);
        dashboard.TotalSavings.Should().Be(0m);
        dashboard.TotalNetWorth.Should().Be(-80m);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WhenCustomTypeOverridesGroup_ClassifiesByCustomGroup()
    {
        var debtType = NewCustomType("Prêt", AccountCategoryGroup.Debt);
        var investmentType = NewCustomType("Titres", AccountCategoryGroup.Investment);
        var liquidType = NewCustomType("Espèces", AccountCategoryGroup.Liquid);
        _context.CustomAccountTypes.AddRange(debtType, investmentType, liquidType);
        var loanNegative = NewAccount("Prêt immo", AccountType.Savings, -900m);
        loanNegative.CustomAccountTypeId = debtType.Id;
        var loanPositive = NewAccount("Prêt remboursé", AccountType.Savings, 100m);
        loanPositive.CustomAccountTypeId = debtType.Id;
        var securities = NewAccount("PEA", AccountType.Checking, 5000m);
        securities.CustomAccountTypeId = investmentType.Id;
        var cash = NewAccount("Coffre", AccountType.Savings, 70m);
        cash.CustomAccountTypeId = liquidType.Id;
        _context.BankAccounts.AddRange(loanNegative, loanPositive, securities, cash);
        await _context.SaveChangesAsync();

        var dashboard = await _service.GetDashboardSummaryAsync();

        dashboard.TotalDebts.Should().Be(900m);
        dashboard.TotalLiquidAssets.Should().Be(170m);
        dashboard.TotalSavings.Should().Be(5000m);
        dashboard.TotalNetWorth.Should().Be(4270m);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WhenTypeTranslationExists_UsesItAsTypeName()
    {
        var customType = NewCustomType("Compte Courant", AccountCategoryGroup.Liquid);
        var other = NewCustomType("Livret", AccountCategoryGroup.Savings);
        _context.CustomAccountTypes.AddRange(customType, other);
        var translated = NewAccount("A", AccountType.Checking, 10m);
        translated.CustomAccountTypeId = customType.Id;
        var untranslated = NewAccount("B", AccountType.Savings, 20m);
        untranslated.CustomAccountTypeId = other.Id;
        var untyped = NewAccount("C", AccountType.Cash, 5m);
        _context.BankAccounts.AddRange(translated, untranslated, untyped);
        await _context.SaveChangesAsync();
        _localizedEntityService
            .Setup(l => l.GetTranslationsAsync("CustomAccountType", It.IsAny<IEnumerable<Guid>>(), "Name", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [customType.Id] = "Checking Account", [other.Id] = " " });

        var dashboard = await _service.GetDashboardSummaryAsync();

        dashboard.Accounts.Single(a => string.Equals(a.AccountName, "A", StringComparison.Ordinal)).CustomAccountTypeName.Should().Be("Checking Account");
        dashboard.Accounts.Single(a => string.Equals(a.AccountName, "B", StringComparison.Ordinal)).CustomAccountTypeName.Should().Be("Livret");
        dashboard.Accounts.Single(a => string.Equals(a.AccountName, "C", StringComparison.Ordinal)).CustomAccountTypeName.Should().BeNull();
        dashboard.Accounts.Single(a => string.Equals(a.AccountName, "A", StringComparison.Ordinal)).CustomAccountTypeColor.Should().Be("#112233");
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WhenAccountsAreCreatedOutOfOrder_ListsThemByName()
    {
        _context.BankAccounts.AddRange(
            NewAccount("livret A", AccountType.Savings, 1m),
            NewAccount("Courant", AccountType.Checking, 1m),
            NewAccount("Assurance vie", AccountType.Savings, 1m),
            NewAccount("PEA", AccountType.Checking, 1m));
        await _context.SaveChangesAsync();

        var dashboard = await _service.GetDashboardSummaryAsync();

        dashboard.Accounts.Select(a => a.AccountName)
            .Should().Equal("Assurance vie", "Courant", "livret A", "PEA");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private BankAccount NewAccount(string name, AccountType type, decimal initialBalance)
    {
        return new BankAccount { Id = Guid.NewGuid(), UserId = UserId, BankId = _bank.Id, Name = name, Type = type, InitialBalance = initialBalance };
    }

    private static CustomAccountType NewCustomType(string name, AccountCategoryGroup group)
    {
        return new CustomAccountType { Id = Guid.NewGuid(), UserId = UserId, Name = name, CategoryGroup = group, ColorHex = "#112233", IconName = "bi-x" };
    }

    private static Transaction NewTransaction(BankAccount account, TransactionType type, decimal amount, TransactionStatus status)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            BankAccountId = account.Id,
            Label = "t",
            Amount = amount,
            Type = type,
            Status = status,
            Date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }
}
