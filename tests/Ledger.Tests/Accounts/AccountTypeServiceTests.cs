using AwesomeAssertions;
using Ledger.Domain.Entities;
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

public sealed class AccountTypeServiceTests : IDisposable
{
    private const string UserId = "test-user-id";

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly AccountTypeService _service;
    private readonly Bank _bank;

    public AccountTypeServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _bank = new Bank { Id = Guid.NewGuid(), UserId = UserId, Name = "LCL" };
        _context.Banks.Add(_bank);
        _context.SaveChanges();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(UserId);
        _service = new AccountTypeService(_context, currentUser.Object, NullLogger<AccountTypeService>.Instance);
    }

    [Fact]
    public async Task EnsureDefaultAccountTypesForUserAsync_WhenUserHasNoType_CreatesSeventeenSystemTypesInOrder()
    {
        await _service.EnsureDefaultAccountTypesForUserAsync(UserId);

        var types = await _context.CustomAccountTypes.OrderBy(t => t.DisplayOrder).ToListAsync();
        types.Should().HaveCount(17);
        types.Select(t => t.DisplayOrder).Should().Equal(Enumerable.Range(1, 17));
        types.Should().OnlyContain(t => t.IsSystem);
        types[0].Code.Should().Be("Checking");
        types[0].Name.Should().Be("Compte de Dépôt");
        types[0].CategoryGroup.Should().Be(AccountCategoryGroup.Liquid);
        types[16].Code.Should().Be("Crypto");
        types[16].CategoryGroup.Should().Be(AccountCategoryGroup.Investment);
    }

    [Fact]
    public async Task EnsureDefaultAccountTypesForUserAsync_WhenAccountsHaveNoType_LinksThemByCode()
    {
        var savings = new BankAccount { Id = Guid.NewGuid(), UserId = UserId, BankId = _bank.Id, Name = "Epargne", Type = AccountType.LivretA };
        var checking = new BankAccount { Id = Guid.NewGuid(), UserId = UserId, BankId = _bank.Id, Name = "Courant", Type = AccountType.Checking };
        _context.BankAccounts.AddRange(savings, checking);
        await _context.SaveChangesAsync();

        await _service.EnsureDefaultAccountTypesForUserAsync(UserId);

        var codes = await _context.BankAccounts
            .Where(a => a.Id == savings.Id || a.Id == checking.Id)
            .Select(a => new { a.Name, a.CustomAccountType!.Code })
            .ToListAsync();
        codes.Should().BeEquivalentTo([new { Name = "Epargne", Code = "LivretA" }, new { Name = "Courant", Code = "Checking" }]);
    }

    [Fact]
    public async Task EnsureDefaultAccountTypesForUserAsync_WhenCreated_AddsEnglishSpanishAndGermanNameTranslations()
    {
        await _service.EnsureDefaultAccountTypesForUserAsync(UserId);

        var checkingId = await _context.CustomAccountTypes.Where(t => t.Code == "Checking").Select(t => t.Id).SingleAsync();
        var translations = await _context.EntityTranslations
            .Where(t => t.EntityType == "CustomAccountType" && t.EntityId == checkingId)
            .ToDictionaryAsync(t => t.LanguageCode, t => t.Translation, StringComparer.Ordinal);
        translations.Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en"] = "Checking Account",
            ["es"] = "Cuenta Corriente",
            ["de"] = "Girokonto"
        });
        (await _context.EntityTranslations.CountAsync(t => t.EntityType == "CustomAccountType")).Should().Be(17 * 3);
    }

    [Fact]
    public async Task GetAllAccountTypesAsync_WhenOneTranslationExists_AddsOnlyMissingOnes()
    {
        var type = new CustomAccountType { Id = Guid.NewGuid(), UserId = UserId, Name = "Livret A", Code = "LivretA" };
        _context.CustomAccountTypes.Add(type);
        _context.EntityTranslations.Add(new EntityTranslation { EntityType = "CustomAccountType", EntityId = type.Id, LanguageCode = "EN", Field = "Name", Translation = "Personnalisé" });
        await _context.SaveChangesAsync();

        await _service.GetAllAccountTypesAsync();

        var translations = await _context.EntityTranslations.Where(t => t.EntityId == type.Id).ToListAsync();
        translations.Should().HaveCount(3);
        translations.Single(t => t.LanguageCode.Equals("en", StringComparison.OrdinalIgnoreCase)).Translation.Should().Be("Personnalisé");
        translations.Single(t => string.Equals(t.LanguageCode, "es", StringComparison.Ordinal)).Translation.Should().Be("Cuenta de ahorro Livret A");
        translations.Single(t => string.Equals(t.LanguageCode, "de", StringComparison.Ordinal)).Translation.Should().Be("Livret A Sparkonto");
    }

    [Fact]
    public async Task GetAllAccountTypesAsync_WhenTypeCodeIsUnknown_AddsNoTranslation()
    {
        var type = new CustomAccountType { Id = Guid.NewGuid(), UserId = UserId, Name = "Perso", Code = "Inconnu" };
        _context.CustomAccountTypes.Add(type);
        await _context.SaveChangesAsync();

        await _service.GetAllAccountTypesAsync();

        (await _context.EntityTranslations.AnyAsync(t => t.EntityId == type.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeduplicateAccountTypesAsync_WhenNoTypeExists_ReturnsZero()
    {
        var deleted = await _service.DeduplicateAccountTypesAsync();

        deleted.Should().Be(0);
    }

    [Fact]
    public async Task DeduplicateAccountTypesAsync_WhenAccountsHaveNoType_LinksThemBeforeSelectingDuplicates()
    {
        var older = NewType("u1", "Compte", "Checking", DateTime.UtcNow.AddHours(-3));
        var newer = NewType("u1", "Compte bis", "Checking", DateTime.UtcNow.AddHours(-1));
        var savingsOlder = NewType("u1", "Livret", "LivretA", DateTime.UtcNow.AddHours(-3));
        var savingsNewer = NewType("u1", "Livret bis", "LivretA", DateTime.UtcNow.AddHours(-1));
        _context.CustomAccountTypes.AddRange(older, newer, savingsOlder, savingsNewer);
        var account = new BankAccount { Id = Guid.NewGuid(), UserId = "u1", BankId = _bank.Id, Name = "A", Type = AccountType.LivretA };
        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync();

        var deleted = await _service.DeduplicateAccountTypesAsync();

        deleted.Should().Be(2);
        var linkedTypeId = await _context.BankAccounts.IgnoreQueryFilters().Where(a => a.Id == account.Id).Select(a => a.CustomAccountTypeId).SingleAsync();
        Guid[] savingsIds = [savingsOlder.Id, savingsNewer.Id];
        linkedTypeId.Should().NotBeNull();
        savingsIds.Should().Contain(linkedTypeId.GetValueOrDefault());
        var remaining = await _context.CustomAccountTypes.IgnoreQueryFilters().Select(t => t.Id).ToListAsync();
        remaining.Should().BeEquivalentTo([older.Id, linkedTypeId.GetValueOrDefault()]);
    }

    [Fact]
    public async Task DeduplicateAccountTypesAsync_WhenAccountTypeCodeIsMissingForUser_LinksAccountToCheckingType()
    {
        var checking = NewType("u1", "Compte", "Checking", DateTime.UtcNow.AddHours(-2));
        var checkingCopy = NewType("u1", "Compte bis", "Checking", DateTime.UtcNow.AddHours(-1));
        _context.CustomAccountTypes.AddRange(checking, checkingCopy);
        var account = new BankAccount { Id = Guid.NewGuid(), UserId = "u1", BankId = _bank.Id, Name = "A", Type = AccountType.Pea };
        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync();

        var deleted = await _service.DeduplicateAccountTypesAsync();

        deleted.Should().Be(1);
        var linkedTypeId = await _context.BankAccounts.IgnoreQueryFilters().Where(a => a.Id == account.Id).Select(a => a.CustomAccountTypeId).SingleAsync();
        linkedTypeId.Should().Be(checking.Id);
    }

    [Fact]
    public async Task DeduplicateAccountTypesAsync_WhenCodeIsBlank_GroupsByTrimmedCaseInsensitiveNameAndRemovesOrphanTranslations()
    {
        var kept = NewType("u1", "Mon Type", null, DateTime.UtcNow.AddHours(-2));
        var duplicate = NewType("u1", "  mon type ", null, DateTime.UtcNow.AddHours(-1));
        var otherUser = NewType("u2", "Mon Type", null, DateTime.UtcNow);
        _context.CustomAccountTypes.AddRange(kept, duplicate, otherUser);
        _context.EntityTranslations.AddRange(
            new EntityTranslation { EntityType = "CustomAccountType", EntityId = duplicate.Id, LanguageCode = "en", Translation = "x" },
            new EntityTranslation { EntityType = "CustomAccountType", EntityId = kept.Id, LanguageCode = "en", Translation = "y" });
        await _context.SaveChangesAsync();

        var deleted = await _service.DeduplicateAccountTypesAsync();

        deleted.Should().Be(1);
        var remaining = await _context.CustomAccountTypes.IgnoreQueryFilters().Select(t => t.Id).ToListAsync();
        remaining.Should().BeEquivalentTo([kept.Id, otherUser.Id]);
        var translatedIds = await _context.EntityTranslations.Select(t => t.EntityId).ToListAsync();
        translatedIds.Should().BeEquivalentTo([kept.Id]);
    }

    [Fact]
    public async Task DeduplicateAccountTypesAsync_WhenNoDuplicateExists_ReturnsZeroAndKeepsEverything()
    {
        _context.CustomAccountTypes.AddRange(NewType("u1", "A", "Checking", DateTime.UtcNow), NewType("u1", "B", "Cash", DateTime.UtcNow));
        await _context.SaveChangesAsync();

        var deleted = await _service.DeduplicateAccountTypesAsync();

        deleted.Should().Be(0);
        (await _context.CustomAccountTypes.IgnoreQueryFilters().CountAsync()).Should().Be(2);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static CustomAccountType NewType(string userId, string name, string? code, DateTime createdAtUtc)
    {
        return new CustomAccountType { Id = Guid.NewGuid(), UserId = userId, Name = name, Code = code, CreatedAtUtc = createdAtUtc };
    }
}
