using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.RulesEngine.Entities;
using Ledger.Features.RulesEngine.Services;
using Ledger.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ledger.Tests.RulesEngine;

public sealed class RulesEngineServiceTests : IDisposable
{
    private readonly Ledger.Infrastructure.Data.AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly RulesEngineService _rulesService;
    private readonly Guid _catSalaryId = Guid.NewGuid();
    private readonly Guid _catGroceriesId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();

    public RulesEngineServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        var inMemoryConfig = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            {"Rules:MaxExecutionTimeMs", "500"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        _rulesService = new RulesEngineService(_context, configuration, NullLogger<RulesEngineService>.Instance);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var catSalary = new Category { Id = _catSalaryId, UserId = "test-user-id", Name = "Salaire", IsIncome = true, IsSalary = true };
        var catGroceries = new Category { Id = _catGroceriesId, UserId = "test-user-id", Name = "Courses", IsIncome = false };

        var ruleSalary = new CategorizationRule
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Règle Salaire",
            Pattern = @"(SALAIRE|VIR.*SOCIETE)",
            TargetCategoryId = _catSalaryId,
            Priority = 1,
            IsActive = true,
            ShiftToNextMonth = true
        };

        var ruleGroceries = new CategorizationRule
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Règle Supermarchés",
            Pattern = @"(CARREFOUR|LECLERC|AUCHAN)",
            TargetCategoryId = _catGroceriesId,
            Priority = 2,
            IsActive = true
        };

        _context.Categories.AddRange(catSalary, catGroceries);
        _context.CategorizationRules.AddRange(ruleSalary, ruleGroceries);
        _context.SaveChanges();
    }

    [Fact]
    public async Task EvaluateAsync_MatchingSalaryRule_ShouldAssignCategoryAndShiftBudgetDate()
    {
        // Arrange : Salaire reçu le 28 janvier
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc),
            Label = "VIREMENT SALAIRE ENTREPRISE",
            Amount = 3000m,
            Type = TransactionType.Credit
        };

        // Act
        var result = await _rulesService.EvaluateAsync(transaction);

        // Assert
        result.IsMatched.Should().BeTrue();
        result.CategoryId.Should().Be(_catSalaryId);
        result.RuleName.Should().Be("Règle Salaire");
        result.ShiftToNextMonth.Should().BeTrue();

        // Application de la règle sur les opérations via le service
        var changedCount = await _rulesService.ApplyRulesToTransactionsAsync(new[] { transaction });
        changedCount.Should().Be(1);

        transaction.CategoryId.Should().Be(_catSalaryId);
        transaction.BudgetDate.Should().NotBeNull();
        // Report au 1er février
        transaction.BudgetDate.Should().Be(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task EvaluateAsync_MatchingGroceries_ShouldAssignCategoryIdWithoutShift()
    {
        // Arrange
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Label = "PAIEMENT CARREFOUR MARKET 75",
            Amount = 52.40m,
            Type = TransactionType.Debit
        };

        // Act
        var result = await _rulesService.EvaluateAsync(transaction);

        // Assert
        result.IsMatched.Should().BeTrue();
        result.CategoryId.Should().Be(_catGroceriesId);
        result.ShiftToNextMonth.Should().BeFalse();

        await _rulesService.ApplyRulesToTransactionsAsync(new[] { transaction });
        transaction.CategoryId.Should().Be(_catGroceriesId);
        transaction.BudgetDate.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_MatchingRuleWithTargetMerchant_ShouldAssignMerchantId()
    {
        // Arrange: créer un marchand et une règle avec TargetMerchantId
        var merchant = new Ledger.Features.Merchants.Entities.Merchant
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Lidl",
            NormalizedName = "lidl"
        };
        _context.Merchants.Add(merchant);

        var ruleLidl = new CategorizationRule
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Règle Lidl",
            Pattern = @"(?i)lidl",
            TargetCategoryId = _catGroceriesId,
            TargetMerchantId = merchant.Id,
            Priority = 5,
            IsActive = true
        };
        _context.CategorizationRules.Add(ruleLidl);
        await _context.SaveChangesAsync();

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
            Label = "PAIEMENT CB LIDL LENS 62",
            Amount = 35.80m,
            Type = TransactionType.Debit
        };

        // Act
        var result = await _rulesService.EvaluateAsync(transaction);

        // Assert
        result.IsMatched.Should().BeTrue();
        result.TargetMerchantId.Should().Be(merchant.Id);

        // Apply
        await _rulesService.ApplyRulesToTransactionsAsync(new[] { transaction });
        transaction.CategoryId.Should().Be(_catGroceriesId);
        transaction.MerchantId.Should().Be(merchant.Id);
    }

    [Fact]
    public async Task GetMatchesForTransactionsAsync_WhenEmptyTransactions_ShouldReturnEmptyDictionary()
    {
        var result = await _rulesService.GetMatchesForTransactionsAsync(Array.Empty<Transaction>());
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMatchesForTransactionsAsync_WhenFiltersApplied_ReturnsDetailedMatches()
    {
        var bank = new Bank { UserId = "test-user-id", Name = "Bank Specific" };
        _context.Banks.Add(bank);
        var specificAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Spécifique"
        };
        _context.BankAccounts.Add(specificAccount);
        await _context.SaveChangesAsync();

        var ruleWithAccountAndAmount = new CategorizationRule
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Gros Salaire Compte Spécifique",
            Pattern = "SALAIRE",
            TargetBankAccountId = specificAccount.Id,
            TargetCategoryId = _catSalaryId,
            MinAmount = 2000m,
            Priority = 2,
            IsActive = true
        };
        _context.CategorizationRules.Add(ruleWithAccountAndAmount);
        await _context.SaveChangesAsync();

        var txMatchingBoth = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = specificAccount.Id,
            Date = DateTime.Today,
            Amount = 2500m,
            Label = "VIR SALAIRE ACME",
            Type = TransactionType.Credit
        };

        var txDifferentAccount = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 2500m,
            Label = "VIR SALAIRE ACME",
            Type = TransactionType.Credit
        };

        var txBelowMinAmount = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = specificAccount.Id,
            Date = DateTime.Today,
            Amount = 500m,
            Label = "VIR SALAIRE ACME",
            Type = TransactionType.Credit
        };

        var txNoMatch = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = specificAccount.Id,
            Date = DateTime.Today,
            Amount = 15m,
            Label = "PHARMACIE CENTRALE",
            Type = TransactionType.Debit
        };

        var matches = await _rulesService.GetMatchesForTransactionsAsync(new[]
        {
            txMatchingBoth,
            txDifferentAccount,
            txBelowMinAmount,
            txNoMatch
        });

        matches.Should().HaveCount(4);

        // txMatchingBoth matches ruleSalary (priority 1) AND ruleWithAccountAndAmount (priority 2) -> 2 matches
        matches[txMatchingBoth.Id].MatchCount.Should().Be(2);
        matches[txMatchingBoth.Id].FirstRuleName.Should().Be("Règle Salaire");
        matches[txMatchingBoth.Id].FirstCategoryName.Should().Be("Salaire");

        // txDifferentAccount matches ruleSalary (priority 1) but NOT ruleWithAccountAndAmount -> 1 match
        matches[txDifferentAccount.Id].MatchCount.Should().Be(1);
        matches[txDifferentAccount.Id].FirstRuleName.Should().Be("Règle Salaire");

        // txBelowMinAmount matches ruleSalary (priority 1) but NOT ruleWithAccountAndAmount (under 2000) -> 1 match
        matches[txBelowMinAmount.Id].MatchCount.Should().Be(1);

        // txNoMatch has 0 matches
        matches[txNoMatch.Id].MatchCount.Should().Be(0);
        matches[txNoMatch.Id].FirstRuleName.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
