using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.RulesEngine.Entities;
using Ledger.Features.RulesEngine.Services;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ledger.Tests.RulesEngine;

public sealed class RulesEngineApplyTests : IDisposable
{
    private const string UserId = "test-user-id";

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly RulesEngineService _service;
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Category _food = new() { Id = Guid.NewGuid(), UserId = UserId, Name = "Courses" };
    private readonly Category _leisure = new() { Id = Guid.NewGuid(), UserId = UserId, Name = "Loisirs" };
    private readonly Merchant _merchant = new() { Id = Guid.NewGuid(), UserId = UserId, Name = "Carrefour", NormalizedName = "CARREFOUR" };

    public RulesEngineApplyTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Rules:MaxExecutionTimeMs"] = "500" }).Build();
        _service = new RulesEngineService(_context, configuration, NullLogger<RulesEngineService>.Instance);
        _context.Categories.AddRange(_food, _leisure);
        _context.Merchants.Add(_merchant);
        _context.SaveChanges();
    }

    [Fact]
    public async Task ApplyRulesToTransactionsAsync_WhenTransactionAlreadyCategorizedAndNotOverwriting_LeavesItUntouched()
    {
        AddRule("Courses", "CARREFOUR", _food.Id, priority: 1);
        var transaction = NewTransaction("CARREFOUR MARKET", 20m, categoryId: _leisure.Id);

        var changed = await _service.ApplyRulesToTransactionsAsync([transaction]);

        changed.Should().Be(0);
        transaction.CategoryId.Should().Be(_leisure.Id);
    }

    [Fact]
    public async Task ApplyRulesToTransactionsAsync_WhenOverwriting_ReplacesCategoryAndCountsChange()
    {
        AddRule("Courses", "CARREFOUR", _food.Id, priority: 1);
        var transaction = NewTransaction("CARREFOUR MARKET", 20m, categoryId: _leisure.Id);

        var changed = await _service.ApplyRulesToTransactionsAsync([transaction], overwriteExisting: true);

        changed.Should().Be(1);
        transaction.CategoryId.Should().Be(_food.Id);
    }

    [Fact]
    public async Task ApplyRulesToTransactionsAsync_WhenResultEqualsCurrentState_ReturnsZero()
    {
        AddRule("Courses", "CARREFOUR", _food.Id, priority: 1);
        var transaction = NewTransaction("CARREFOUR MARKET", 20m, categoryId: _food.Id);

        var changed = await _service.ApplyRulesToTransactionsAsync([transaction], overwriteExisting: true);

        changed.Should().Be(0);
    }

    [Fact]
    public async Task ApplyRulesToTransactionsAsync_WhenSeveralRulesMatch_AppliesOnlyTheFirstByPriority()
    {
        AddRule("Loisirs", "MARKET", _leisure.Id, priority: 2);
        AddRule("Courses", "CARREFOUR", _food.Id, priority: 1);
        var transaction = NewTransaction("CARREFOUR MARKET", 20m);

        await _service.ApplyRulesToTransactionsAsync([transaction]);

        transaction.CategoryId.Should().Be(_food.Id);
    }

    [Fact]
    public async Task ApplyRulesToTransactionsAsync_WhenSingleRuleIdGiven_UsesOnlyThatRule()
    {
        AddRule("Courses", "CARREFOUR", _food.Id, priority: 1);
        var leisure = AddRule("Loisirs", "CARREFOUR", _leisure.Id, priority: 2);
        var transaction = NewTransaction("CARREFOUR MARKET", 20m);

        var changed = await _service.ApplyRulesToTransactionsAsync([transaction], overwriteExisting: false, singleRuleId: leisure.Id);

        changed.Should().Be(1);
        transaction.CategoryId.Should().Be(_leisure.Id);
    }

    [Fact]
    public async Task ApplyRulesToTransactionsAsync_WhenAmountOrAccountOutsideRuleScope_IgnoresRule()
    {
        var rule = AddRule("Courses", "CARREFOUR", _food.Id, priority: 1);
        rule.MinAmount = 10m;
        rule.MaxAmount = 50m;
        var other = AddRule("Autre compte", "AUCHAN", _leisure.Id, priority: 2);
        other.TargetBankAccountId = Guid.NewGuid();
        var tooSmall = NewTransaction("CARREFOUR MARKET", 5m);
        var tooBig = NewTransaction("CARREFOUR MARKET", 80m);
        var inRange = NewTransaction("CARREFOUR MARKET", 50m);
        var wrongAccount = NewTransaction("AUCHAN DRIVE", 20m);

        var changed = await _service.ApplyRulesToTransactionsAsync([tooSmall, tooBig, inRange, wrongAccount]);

        changed.Should().Be(1);
        tooSmall.CategoryId.Should().BeNull();
        tooBig.CategoryId.Should().BeNull();
        inRange.CategoryId.Should().Be(_food.Id);
        wrongAccount.CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task ApplyRulesToTransactionsAsync_WhenRuleTargetsMerchant_AssignsItOnlyIfMissingUnlessOverwriting()
    {
        var rule = AddRule("Courses", "CARREFOUR", _food.Id, priority: 1);
        rule.TargetMerchantId = _merchant.Id;
        var otherMerchantId = Guid.NewGuid();
        var withoutMerchant = NewTransaction("CARREFOUR MARKET", 20m);
        var withMerchant = NewTransaction("CARREFOUR CITY", 20m);
        withMerchant.MerchantId = otherMerchantId;

        await _service.ApplyRulesToTransactionsAsync([withoutMerchant, withMerchant]);
        withoutMerchant.MerchantId.Should().Be(_merchant.Id);
        withMerchant.MerchantId.Should().Be(otherMerchantId);

        withMerchant.CategoryId = null;
        var changed = await _service.ApplyRulesToTransactionsAsync([withMerchant], overwriteExisting: true);

        changed.Should().Be(1);
        withMerchant.MerchantId.Should().Be(_merchant.Id);
    }

    [Fact]
    public async Task ApplyRulesToTransactionsAsync_WhenRuleShiftsToNextMonth_SetsFirstDayOfFollowingMonth()
    {
        var rule = AddRule("Salaire", "SALAIRE", _food.Id, priority: 1);
        rule.ShiftToNextMonth = true;
        var transaction = NewTransaction("SALAIRE DECEMBRE", 2000m, date: new DateTime(2026, 12, 28, 0, 0, 0, DateTimeKind.Utc));

        await _service.ApplyRulesToTransactionsAsync([transaction]);

        transaction.BudgetDate.Should().Be(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ApplyRulesToTransactionsAsync_WhenFirstRulePatternIsInvalid_FallsBackToNextRule()
    {
        AddRule("Cassée", "(", _leisure.Id, priority: 1);
        AddRule("Courses", "CARREFOUR", _food.Id, priority: 2);
        var transaction = NewTransaction("CARREFOUR MARKET", 20m);

        var changed = await _service.ApplyRulesToTransactionsAsync([transaction]);

        changed.Should().Be(1);
        transaction.CategoryId.Should().Be(_food.Id);
    }

    [Fact]
    public async Task ApplyRulesToTransactionsAsync_WhenNoActiveRule_ReturnsZero()
    {
        var rule = AddRule("Inactive", "CARREFOUR", _food.Id, priority: 1);
        rule.IsActive = false;
        await _context.SaveChangesAsync();
        var transaction = NewTransaction("CARREFOUR MARKET", 20m);

        var changed = await _service.ApplyRulesToTransactionsAsync([transaction]);

        changed.Should().Be(0);
    }

    [Fact]
    public async Task GetMatchesForTransactionsAsync_WhenSeveralRulesMatch_ReportsCountNamesInPriorityOrderAndFirstTargets()
    {
        var first = AddRule("Courses", "CARREFOUR", _food.Id, priority: 1);
        first.TargetMerchantId = _merchant.Id;
        AddRule("Loisirs", "MARKET", _leisure.Id, priority: 2);
        var matching = NewTransaction("CARREFOUR MARKET", 20m);
        var nonMatching = NewTransaction("BOULANGERIE", 3m);

        var matches = await _service.GetMatchesForTransactionsAsync([matching, nonMatching]);

        var match = matches[matching.Id];
        match.MatchCount.Should().Be(2);
        match.MatchingRuleNames.Should().Equal("Courses", "Loisirs");
        match.FirstRuleName.Should().Be("Courses");
        match.FirstTargetCategoryId.Should().Be(_food.Id);
        match.FirstCategoryName.Should().Be("Courses");
        match.FirstTargetMerchantId.Should().Be(_merchant.Id);
        match.FirstMerchantName.Should().Be("Carrefour");
        matches[nonMatching.Id].MatchCount.Should().Be(0);
        matches[nonMatching.Id].FirstRuleName.Should().BeNull();
    }

    [Fact]
    public async Task GetMatchesForTransactionsAsync_WhenNoActiveRule_ReturnsZeroCountForEachTransaction()
    {
        var transaction = NewTransaction("CARREFOUR MARKET", 20m);

        var matches = await _service.GetMatchesForTransactionsAsync([transaction]);

        matches.Should().ContainKey(transaction.Id).WhoseValue.MatchCount.Should().Be(0);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private CategorizationRule AddRule(string name, string pattern, Guid categoryId, int priority)
    {
        var rule = new CategorizationRule
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Name = name,
            Pattern = pattern,
            TargetCategoryId = categoryId,
            Priority = priority,
            IsActive = true
        };
        _context.CategorizationRules.Add(rule);
        _context.SaveChanges();
        return rule;
    }

    private Transaction NewTransaction(string label, decimal amount, Guid? categoryId = null, DateTime? date = null)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            BankAccountId = _accountId,
            Label = label,
            Amount = amount,
            Type = TransactionType.Debit,
            CategoryId = categoryId,
            Date = date ?? new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc)
        };
    }
}
