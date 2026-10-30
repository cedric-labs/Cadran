using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Merchants.Services;
using Ledger.Features.RulesEngine.Entities;
using Ledger.Features.Security.Context;
using Ledger.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ledger.Tests.Merchants;

public sealed class MerchantServiceTests : IDisposable
{
    private readonly Ledger.Infrastructure.Data.AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly MerchantService _service;
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();

    public MerchantServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create("test-user");
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCurrentUserService.Setup(u => u.UserId).Returns("test-user");
        _service = new MerchantService(_context, _mockCurrentUserService.Object, NullLogger<MerchantService>.Instance, TimeProvider.System);

        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user", Name = "Banque Test" };
        var account = new BankAccount { Id = _bankAccountId, UserId = "test-user", BankId = bank.Id, Name = "Compte Courant" };
        var category = new Category { Id = _categoryId, UserId = "test-user", Name = "Général" };
        _context.Banks.Add(bank);
        _context.BankAccounts.Add(account);
        _context.Categories.Add(category);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistMerchant_WithNormalizedName()
    {
        // Act
        var merchant = await _service.CreateAsync("  Lidl France  ", iconName: "bi-shop", colorHex: "#ff0000");

        // Assert
        merchant.Should().NotBeNull();
        merchant.Name.Should().Be("Lidl France");
        merchant.NormalizedName.Should().Be("lidl france");
        merchant.IconName.Should().Be("bi-shop");
        merchant.ColorHex.Should().Be("#ff0000");

        var inDb = await _context.Merchants.FindAsync(merchant.Id);
        inDb.Should().NotBeNull();
        inDb.Name.Should().Be("Lidl France");
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicateNormalizedName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await _service.CreateAsync("Carrefour");

        // Act & Assert
        var act = async () => await _service.CreateAsync("carrefour");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*existe déjà*");
    }

    [Fact]
    public async Task GetOrCreateByNameAsync_WhenNotExists_ShouldCreateNew()
    {
        // Act
        var merchant = await _service.GetOrCreateByNameAsync("Auchan");

        // Assert
        merchant.Should().NotBeNull();
        merchant.Name.Should().Be("Auchan");
        merchant.NormalizedName.Should().Be("auchan");

        var all = await _service.GetAllMerchantsAsync();
        all.Should().ContainSingle(m => m.Name == "Auchan");
    }

    [Fact]
    public async Task GetOrCreateByNameAsync_WhenExistsCaseInsensitive_ShouldReturnExistingWithoutDuplication()
    {
        // Arrange
        var initial = await _service.CreateAsync("TotalEnergies");

        // Act
        var found = await _service.GetOrCreateByNameAsync("  totalenergies  ");

        // Assert
        found.Should().NotBeNull();
        found.Id.Should().Be(initial.Id);
        found.Name.Should().Be("TotalEnergies");

        var all = await _service.GetAllMerchantsAsync();
        all.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateFields_AndRecomputeNormalizedName()
    {
        // Arrange
        var merchant = await _service.CreateAsync("Amazon FR");

        // Act
        var updated = await _service.UpdateAsync(merchant.Id, "Amazon Prime", colorHex: "#ff9900");

        // Assert
        updated.Should().NotBeNull();
        updated.NormalizedName.Should().Be("amazon prime");
        updated.ColorHex.Should().Be("#ff9900");

        var inDb = await _context.Merchants.FindAsync(merchant.Id);
        inDb!.Name.Should().Be("Amazon Prime");
        inDb.NormalizedName.Should().Be("amazon prime");
    }

    [Fact]
    public async Task DeleteAsync_WhenCalled_DetachesFromTransactionsAndRules()
    {
        // Arrange
        var merchant = await _service.CreateAsync("Netflix");

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 15.99m,
            Label = "Abonnement Netflix",
            Type = TransactionType.Debit,
            MerchantId = merchant.Id
        };
        _context.Transactions.Add(tx);

        var rule = new CategorizationRule
        {
            Id = Guid.NewGuid(),
            UserId = "test-user",
            Name = "Netflix rule",
            Pattern = "(?i)netflix",
            TargetCategoryId = _categoryId,
            TargetMerchantId = merchant.Id
        };
        _context.CategorizationRules.Add(rule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteAsync(merchant.Id);

        // Assert
        result.Should().BeTrue();
        (await _context.Merchants.FindAsync(merchant.Id)).Should().BeNull();

        var reloadedTx = await _context.Transactions.FindAsync(tx.Id);
        reloadedTx!.MerchantId.Should().BeNull();

        var reloadedRule = await _context.CategorizationRules.FindAsync(rule.Id);
        reloadedRule!.TargetMerchantId.Should().BeNull();
    }

    [Fact]
    public async Task MergeMerchantsAsync_ShouldReassignTransactionsAndRules_AndRemoveSource()
    {
        // Arrange
        var sourceMerchant = await _service.CreateAsync("Lidl Lens");
        var targetMerchant = await _service.CreateAsync("Lidl");

        var tx1 = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 45m,
            Label = "Lidl Lens 62",
            Type = TransactionType.Debit,
            MerchantId = sourceMerchant.Id
        };
        var tx2 = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 30m,
            Label = "Lidl Autre",
            Type = TransactionType.Debit,
            MerchantId = targetMerchant.Id
        };
        _context.Transactions.AddRange(tx1, tx2);

        var rule = new CategorizationRule
        {
            Id = Guid.NewGuid(),
            UserId = "test-user",
            Name = "Lidl Rule",
            Pattern = "(?i)lidl",
            TargetCategoryId = _categoryId,
            TargetMerchantId = sourceMerchant.Id
        };
        _context.CategorizationRules.Add(rule);
        await _context.SaveChangesAsync();

        // Act
        var reallocatedCount = await _service.MergeMerchantsAsync(sourceMerchant.Id, targetMerchant.Id);

        // Assert
        reallocatedCount.Should().Be(1);

        var inDbSource = await _context.Merchants.FindAsync(sourceMerchant.Id);
        inDbSource.Should().BeNull();

        var reloadedTx1 = await _context.Transactions.FindAsync(tx1.Id);
        reloadedTx1!.MerchantId.Should().Be(targetMerchant.Id);

        var reloadedRule = await _context.CategorizationRules.FindAsync(rule.Id);
        reloadedRule!.TargetMerchantId.Should().Be(targetMerchant.Id);
    }

    [Fact]
    public async Task GetTransactionCountsByMerchantAsync_WhenCalled_ReturnsAccurateCounts()
    {
        // Arrange
        var m1 = await _service.CreateAsync("Boulangerie");
        var m2 = await _service.CreateAsync("Pharmacie");

        _context.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), UserId = "test-user", BankAccountId = _bankAccountId, Date = DateTime.Today, Amount = 5m, Label = "Pain", Type = TransactionType.Debit, MerchantId = m1.Id },
            new Transaction { Id = Guid.NewGuid(), UserId = "test-user", BankAccountId = _bankAccountId, Date = DateTime.Today, Amount = 3m, Label = "Croissant", Type = TransactionType.Debit, MerchantId = m1.Id },
            new Transaction { Id = Guid.NewGuid(), UserId = "test-user", BankAccountId = _bankAccountId, Date = DateTime.Today, Amount = 12m, Label = "Médicaments", Type = TransactionType.Debit, MerchantId = m2.Id }
        );
        await _context.SaveChangesAsync();

        // Act
        var counts = await _service.GetTransactionCountsByMerchantAsync();

        // Assert
        counts[m1.Id].Should().Be(2);
        counts[m2.Id].Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_WhenUpdated_SetsUpdatedAtUtcFromTimeProvider()
    {
        var fixedUtc = new DateTimeOffset(2025, 3, 15, 14, 20, 0, TimeSpan.Zero);
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(fixedUtc);
        var service = new MerchantService(_context, _mockCurrentUserService.Object, NullLogger<MerchantService>.Instance, fakeTime);

        var merchant = await service.CreateAsync("Boulangerie Initiale");

        var updated = await service.UpdateAsync(merchant.Id, "Boulangerie Renommée", _categoryId, null, null);

        updated.Should().NotBeNull();
        updated.UpdatedAtUtc.Should().Be(fixedUtc.UtcDateTime);
    }
}
