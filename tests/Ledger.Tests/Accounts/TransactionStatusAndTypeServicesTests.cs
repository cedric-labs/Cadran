using FluentAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Security.Context;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ledger.Tests.Accounts;

public class TransactionStatusAndTypeServicesTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly TransactionStatusService _statusService;
    private readonly AccountTypeService _accountTypeService;
    private readonly Bank _bank;

    public TransactionStatusAndTypeServicesTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        _bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "LCL" };
        _context.Banks.Add(_bank);
        _context.SaveChanges();

        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCurrentUserService.Setup(u => u.UserId).Returns("test-user-id");

        _statusService = new TransactionStatusService(
            _context,
            _mockCurrentUserService.Object,
            NullLogger<TransactionStatusService>.Instance);

        _accountTypeService = new AccountTypeService(
            _context,
            _mockCurrentUserService.Object,
            NullLogger<AccountTypeService>.Instance);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _context.Dispose();
        _connection.Dispose();
    }

    // =========================================================================
    // TRANSACTION STATUS SERVICE TESTS
    // =========================================================================

    [Fact]
    public async Task GetAllStatusesAsync_WhenEmpty_SeedsDefaultsAndReturnsList()
    {
        var statuses = await _statusService.GetAllStatusesAsync();

        statuses.Should().HaveCount(3);
        statuses.Should().Contain(s => s.Code == "Cleared" && s.IsDefault);
        statuses.Should().Contain(s => s.Code == "Reconciled");
        statuses.Should().Contain(s => s.Code == "Pending");
    }

    [Fact]
    public async Task StatusService_GetDefaultStatusAsync_ShouldReturnDefaultStatus()
    {
        var def = await _statusService.GetDefaultStatusAsync();

        def.Should().NotBeNull();
        def!.Code.Should().Be("Cleared");
        def.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenMarkedDefault_ResetsOtherDefaults()
    {
        await _statusService.GetAllStatusesAsync(); // seed defaults

        var newStatus = new TransactionCustomStatus
        {
            UserId = "test-user-id",
            Name = "En cours d'analyse",
            Code = "Analysing",
            IsDefault = true
        };

        var created = await _statusService.CreateAsync(newStatus);

        created.DisplayOrder.Should().BeGreaterThan(3);

        var currentDefault = await _statusService.GetDefaultStatusAsync();
        currentDefault!.Id.Should().Be(created.Id);

        var previousDefault = (await _statusService.GetAllStatusesAsync()).FirstOrDefault(s => s.Code == "Cleared");
        previousDefault!.IsDefault.Should().BeFalse();
    }

    [Fact]
    public Task UpdateAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var fake = new TransactionCustomStatus { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Fantôme" };
        var act = async () => await _statusService.UpdateAsync(fake);

        return act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenValid_UpdatesPropertiesAndResetsOtherDefaults()
    {
        var statuses = await _statusService.GetAllStatusesAsync();
        var pending = statuses.First(s => s.Code == "Pending");

        var updateRequest = new TransactionCustomStatus
        {
            Id = pending.Id,
            UserId = pending.UserId,
            Name = "En cours de traitement",
            Description = "Nouvelle description",
            ColorHex = "#abcdef",
            IconName = "bi-gear",
            IsDefault = true
        };

        var updated = await _statusService.UpdateAsync(updateRequest);

        updated.Name.Should().Be("En cours de traitement");
        updated.Description.Should().Be("Nouvelle description");
        updated.ColorHex.Should().Be("#abcdef");
        updated.IconName.Should().Be("bi-gear");
        updated.IsDefault.Should().BeTrue();

        var cleared = statuses.First(s => s.Code == "Cleared");
        _context.Entry(cleared).Reload();
        cleared.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenSystemOrInUse_PreventsDeletion()
    {
        var statuses = await _statusService.GetAllStatusesAsync();
        var cleared = statuses.First(s => s.Code == "Cleared");

        // System status
        var (sysSuccess, sysErr) = await _statusService.DeleteAsync(cleared.Id);
        sysSuccess.Should().BeFalse();
        sysErr.Should().Contain("base");

        // Custom status in use
        var custom = await _statusService.CreateAsync(new TransactionCustomStatus
        {
            UserId = "test-user-id",
            Name = "À valider",
            IsSystem = false
        });

        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Compte Test"
        };
        _context.BankAccounts.Add(account);
        _context.Transactions.Add(new Transaction
        {
            UserId = "test-user-id",
            BankAccountId = account.Id,
            Date = DateTime.Today,
            Amount = 10m,
            Label = "Test",
            CustomStatusId = custom.Id
        });
        await _context.SaveChangesAsync();

        var (useSuccess, useErr) = await _statusService.DeleteAsync(custom.Id);
        useSuccess.Should().BeFalse();
        useErr.Should().Contain("utilisé");

        var count = await _statusService.GetTransactionCountForStatusAsync(custom.Id);
        count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_WhenValidCustomStatus_DeletesStatus()
    {
        var custom = await _statusService.CreateAsync(new TransactionCustomStatus
        {
            UserId = "test-user-id",
            Name = "Statut Temporaire",
            IsSystem = false
        });

        var (success, err) = await _statusService.DeleteAsync(custom.Id);

        success.Should().BeTrue();
        err.Should().BeNull();
        (await _statusService.GetByIdAsync(custom.Id)).Should().BeNull();
    }

    [Fact]
    public async Task StatusService_EnsureDefaultStatuses_ShouldBackfillTransactionsWithoutStatus()
    {
        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Compte Backfill"
        };
        _context.BankAccounts.Add(account);

        var tx = new Transaction
        {
            UserId = "test-user-id",
            BankAccountId = account.Id,
            Date = DateTime.Today,
            Amount = 30m,
            Status = TransactionStatus.Reconciled,
            CustomStatusId = null,
            Label = "Tx sans statut personnalisé"
        };
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        await _statusService.EnsureDefaultStatusesForUserAsync("test-user-id");

        var refreshed = await _context.Transactions.FindAsync(tx.Id);
        refreshed!.CustomStatusId.Should().NotBeNull();
        var status = await _context.TransactionStatuses.FindAsync(refreshed.CustomStatusId);
        status!.Code.Should().Be("Reconciled");
    }

    // =========================================================================
    // ACCOUNT TYPE SERVICE TESTS
    // =========================================================================

    [Fact]
    public async Task AccountTypeService_GetAllAccountTypesAsync_ShouldSeedDefaultsAndReturn()
    {
        var types = await _accountTypeService.GetAllAccountTypesAsync();

        types.Should().NotBeEmpty();
        types.Should().Contain(t => t.Code == "Checking");
        types.Should().Contain(t => t.Code == "LivretA");
        types.Should().Contain(t => t.Code == "Pea");
    }

    [Fact]
    public async Task CreateAndSaveAsync_WhenValid_PersistsChanges()
    {
        var created = await _accountTypeService.CreateAsync(new CustomAccountType
        {
            UserId = "test-user-id",
            Name = "Compte Épargne Entreprise (PEE)",
            Code = "Pee",
            CategoryGroup = AccountCategoryGroup.Savings
        });

        created.Id.Should().NotBeEmpty();
        created.DisplayOrder.Should().BeGreaterThan(0);

        created.Name = "Plan Épargne Entreprise Modifié";
        var updated = await _accountTypeService.UpdateAsync(created);
        updated.Name.Should().Be("Plan Épargne Entreprise Modifié");

        var retrieved = await _accountTypeService.GetByIdAsync(created.Id);
        retrieved!.Name.Should().Be("Plan Épargne Entreprise Modifié");
    }

    [Fact]
    public async Task DeleteTypeAsync_WhenSystemOrInUse_PreventsDeletion()
    {
        var types = await _accountTypeService.GetAllAccountTypesAsync();
        var checking = types.First(t => t.Code == "Checking");

        // System type cannot be deleted
        var (sysSuccess, _) = await _accountTypeService.DeleteAsync(checking.Id);
        sysSuccess.Should().BeFalse();

        // Custom type in use
        var custom = await _accountTypeService.CreateAsync(new CustomAccountType
        {
            UserId = "test-user-id",
            Name = "Type Utilisé",
            IsSystem = false
        });

        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Compte Test Type",
            CustomAccountTypeId = custom.Id
        };
        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync();

        var (useSuccess, useErr) = await _accountTypeService.DeleteAsync(custom.Id);
        useSuccess.Should().BeFalse();
        useErr.Should().Contain("associé");
    }

    [Fact]
    public async Task DeleteTypeAsync_WhenUnusedCustomType_DeletesSuccessfully()
    {
        var custom = await _accountTypeService.CreateAsync(new CustomAccountType
        {
            UserId = "test-user-id",
            Name = "Type Inutilisé",
            IsSystem = false
        });

        var (success, _) = await _accountTypeService.DeleteAsync(custom.Id);
        success.Should().BeTrue();
        (await _accountTypeService.GetByIdAsync(custom.Id)).Should().BeNull();
    }

    [Fact]
    public async Task UpdateTypeAsync_WhenValid_UpdatesPropertiesSuccessfully()
    {
        var types = await _accountTypeService.GetAllAccountTypesAsync();
        var checking = types.First(t => t.Code == "Checking");

        checking.Name = "Compte Courant Modifié";
        checking.Description = "Nouvelle desc";
        checking.ColorHex = "#123456";
        checking.IconName = "bi-cash";
        checking.CategoryGroup = AccountCategoryGroup.Liquid;

        var updated = await _accountTypeService.UpdateAsync(checking);
        updated.Name.Should().Be("Compte Courant Modifié");
        updated.Description.Should().Be("Nouvelle desc");
        updated.ColorHex.Should().Be("#123456");
        updated.IconName.Should().Be("bi-cash");

        var actNotFound = async () => await _accountTypeService.UpdateAsync(new CustomAccountType { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Inconnu" });
        await actNotFound.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task EnsureDefaultAccountTypesForUserAsync_WhenTypesAlreadyExist_DoesNotInsertDuplicates()
    {
        await _accountTypeService.EnsureDefaultAccountTypesForUserAsync("test-user-id");
        var initialCount = await _context.CustomAccountTypes.CountAsync();
        initialCount.Should().BeGreaterThan(0);

        // Deuxième appel idempotent
        await _accountTypeService.EnsureDefaultAccountTypesForUserAsync("test-user-id");
        var postCount = await _context.CustomAccountTypes.CountAsync();

        postCount.Should().Be(initialCount);
    }

    [Fact]
    public async Task DeduplicateAccountTypesAsync_WhenDuplicatesExistAndSomeUsedByAccounts_PreservesUsedTypesAndDeletesUnusedDuplicates()
    {
        var usedType = new CustomAccountType
        {
            Id = Guid.NewGuid(),
            UserId = "user-dedup",
            Name = "Compte Courant",
            Code = "Checking",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        var unusedDuplicate1 = new CustomAccountType
        {
            Id = Guid.NewGuid(),
            UserId = "user-dedup",
            Name = "Compte Courant",
            Code = "Checking",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        var unusedDuplicate2 = new CustomAccountType
        {
            Id = Guid.NewGuid(),
            UserId = "user-dedup",
            Name = "Compte Courant",
            Code = "Checking",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unusedSavings1 = new CustomAccountType
        {
            Id = Guid.NewGuid(),
            UserId = "user-dedup",
            Name = "Livret A",
            Code = "LivretA",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        var unusedSavings2 = new CustomAccountType
        {
            Id = Guid.NewGuid(),
            UserId = "user-dedup",
            Name = "Livret A",
            Code = "LivretA",
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.CustomAccountTypes.AddRange(usedType, unusedDuplicate1, unusedDuplicate2, unusedSavings1, unusedSavings2);

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "user-dedup",
            BankId = _bank.Id,
            Name = "Mon Compte Principal",
            CustomAccountTypeId = usedType.Id
        };
        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync();

        var deletedCount = await _accountTypeService.DeduplicateAccountTypesAsync();

        // Deux doublons Checking supprimés + un doublon LivretA supprimé = 3
        deletedCount.Should().Be(3);

        // Le type utilisé par le compte doit impérativement être conservé
        var survivingUsed = await _context.CustomAccountTypes.FindAsync(usedType.Id);
        survivingUsed.Should().NotBeNull();

        // Les doublons inutilisés doivent être supprimés
        (await _context.CustomAccountTypes.FindAsync(unusedDuplicate1.Id)).Should().BeNull();
        (await _context.CustomAccountTypes.FindAsync(unusedDuplicate2.Id)).Should().BeNull();
        (await _context.CustomAccountTypes.FindAsync(unusedSavings2.Id)).Should().BeNull();

        // L'exemplaire canonique du Livret A doit être conservé
        (await _context.CustomAccountTypes.FindAsync(unusedSavings1.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeduplicateStatusesAsync_WhenDuplicatesExistAndSomeUsedByTransactions_PreservesUsedStatusesAndDeletesUnusedDuplicates()
    {
        var usedStatus = new TransactionCustomStatus
        {
            Id = Guid.NewGuid(),
            UserId = "user-dedup-status",
            Name = "Comptabilisée",
            Code = "Cleared",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        var unusedStatus = new TransactionCustomStatus
        {
            Id = Guid.NewGuid(),
            UserId = "user-dedup-status",
            Name = "Comptabilisée",
            Code = "Cleared",
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.TransactionStatuses.AddRange(usedStatus, unusedStatus);

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "user-dedup-status",
            BankId = _bank.Id,
            Name = "Compte Status"
        };
        _context.BankAccounts.Add(account);

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "user-dedup-status",
            BankAccountId = account.Id,
            Date = DateTime.Today,
            Label = "Test Tx",
            Amount = -25m,
            CustomStatusId = usedStatus.Id
        };
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        var deleted = await _statusService.DeduplicateStatusesAsync();
        deleted.Should().Be(1);

        (await _context.TransactionStatuses.FindAsync(usedStatus.Id)).Should().NotBeNull();
        (await _context.TransactionStatuses.FindAsync(unusedStatus.Id)).Should().BeNull();
    }
}
