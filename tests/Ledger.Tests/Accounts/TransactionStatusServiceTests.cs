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

public sealed class TransactionStatusServiceTests : IDisposable
{
    private const string UserId = "test-user-id";

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly TransactionStatusService _service;

    public TransactionStatusServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(UserId);
        _service = new TransactionStatusService(_context, currentUser.Object, NullLogger<TransactionStatusService>.Instance);
    }

    [Fact]
    public async Task EnsureDefaultStatusesForUserAsync_WhenUserHasNoStatus_CreatesThreeSystemStatuses()
    {
        await _service.EnsureDefaultStatusesForUserAsync(UserId);

        var statuses = await _context.TransactionStatuses.OrderBy(s => s.DisplayOrder).ToListAsync();
        statuses.Select(s => (s.Code, s.Name, s.IsDefault, s.ColorHex, s.IconName)).Should().Equal(
            ("Reconciled", "Pointée", false, "#198754", "bi-check2-all"),
            ("Cleared", "Comptabilisée", true, "#0dcaf0", "bi-check-lg"),
            ("Pending", "En attente", false, "#ffc107", "bi-hourglass-split"));
        statuses.Should().OnlyContain(s => s.IsSystem);
    }

    [Fact]
    public async Task EnsureDefaultStatusesForUserAsync_WhenTransactionsHaveNoCustomStatus_MapsLegacyStatusToDefaultStatus()
    {
        var bank = new Bank { Id = Guid.NewGuid(), UserId = UserId, Name = "LCL" };
        var account = new BankAccount { Id = Guid.NewGuid(), UserId = UserId, BankId = bank.Id, Name = "Courant" };
        _context.Banks.Add(bank);
        _context.BankAccounts.Add(account);
        var reconciled = NewTransaction(account.Id, TransactionStatus.Reconciled);
        var pending = NewTransaction(account.Id, TransactionStatus.Pending);
        var cleared = NewTransaction(account.Id, TransactionStatus.Cleared);
        _context.Transactions.AddRange(reconciled, pending, cleared);
        await _context.SaveChangesAsync();

        await _service.EnsureDefaultStatusesForUserAsync(UserId);

        var customStatuses = await _context.Transactions
            .Where(t => t.Id == reconciled.Id || t.Id == pending.Id || t.Id == cleared.Id)
            .ToDictionaryAsync(t => t.Id, t => t.CustomStatusId);
        var codeById = await _context.TransactionStatuses.ToDictionaryAsync(st => st.Id, st => st.Code);
        var codes = customStatuses.ToDictionary(kv => kv.Key, kv => codeById[kv.Value.GetValueOrDefault()]);
        codes[reconciled.Id].Should().Be("Reconciled");
        codes[pending.Id].Should().Be("Pending");
        codes[cleared.Id].Should().Be("Cleared");
    }

    [Fact]
    public async Task EnsureDefaultStatusesForUserAsync_WhenCreated_AddsEnglishSpanishAndGermanNameTranslations()
    {
        await _service.EnsureDefaultStatusesForUserAsync(UserId);

        var pendingId = await _context.TransactionStatuses.Where(s => s.Code == "Pending").Select(s => s.Id).SingleAsync();
        var translations = await _context.EntityTranslations
            .Where(t => t.EntityType == "TransactionCustomStatus" && t.EntityId == pendingId && t.Field == "Name")
            .ToDictionaryAsync(t => t.LanguageCode, t => t.Translation, StringComparer.Ordinal);
        translations.Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en"] = "Pending",
            ["es"] = "Pendiente",
            ["de"] = "Ausstehend"
        });
        (await _context.EntityTranslations.CountAsync(t => t.EntityType == "TransactionCustomStatus")).Should().Be(9);
    }

    [Fact]
    public async Task GetAllStatusesAsync_WhenOneTranslationExists_AddsOnlyMissingOnes()
    {
        var status = new TransactionCustomStatus { Id = Guid.NewGuid(), UserId = UserId, Name = "Pointée", Code = "Reconciled" };
        _context.TransactionStatuses.Add(status);
        _context.EntityTranslations.Add(new EntityTranslation { EntityType = "TransactionCustomStatus", EntityId = status.Id, LanguageCode = "ES", Field = "Name", Translation = "Personalizada" });
        await _context.SaveChangesAsync();

        await _service.GetAllStatusesAsync();

        var translations = await _context.EntityTranslations.Where(t => t.EntityId == status.Id).ToListAsync();
        translations.Should().HaveCount(3);
        translations.Single(t => t.LanguageCode.Equals("es", StringComparison.OrdinalIgnoreCase)).Translation.Should().Be("Personalizada");
        translations.Single(t => string.Equals(t.LanguageCode, "en", StringComparison.Ordinal)).Translation.Should().Be("Reconciled");
        translations.Single(t => string.Equals(t.LanguageCode, "de", StringComparison.Ordinal)).Translation.Should().Be("Abgeglichen");
    }

    [Fact]
    public async Task GetAllStatusesAsync_WhenStatusCodeIsUnknown_AddsNoTranslation()
    {
        var status = new TransactionCustomStatus { Id = Guid.NewGuid(), UserId = UserId, Name = "Perso", Code = "Inconnu" };
        _context.TransactionStatuses.Add(status);
        await _context.SaveChangesAsync();

        await _service.GetAllStatusesAsync();

        (await _context.EntityTranslations.AnyAsync(t => t.EntityId == status.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeduplicateStatusesAsync_WhenNoStatusExists_ReturnsZero()
    {
        var deleted = await _service.DeduplicateStatusesAsync();

        deleted.Should().Be(0);
    }

    [Fact]
    public async Task DeduplicateStatusesAsync_WhenNoneIsUsed_KeepsOldestPerUserAndRemovesOrphanTranslations()
    {
        var kept = NewStatus("u1", "Mon Statut", null, DateTime.UtcNow.AddHours(-2));
        var duplicate = NewStatus("u1", "  mon statut ", null, DateTime.UtcNow.AddHours(-1));
        var otherUser = NewStatus("u2", "Mon Statut", null, DateTime.UtcNow);
        var distinct = NewStatus("u1", "Autre", "Cleared", DateTime.UtcNow);
        _context.TransactionStatuses.AddRange(kept, duplicate, otherUser, distinct);
        _context.EntityTranslations.AddRange(
            new EntityTranslation { EntityType = "TransactionCustomStatus", EntityId = duplicate.Id, LanguageCode = "en", Translation = "x" },
            new EntityTranslation { EntityType = "TransactionCustomStatus", EntityId = kept.Id, LanguageCode = "en", Translation = "y" });
        await _context.SaveChangesAsync();

        var deleted = await _service.DeduplicateStatusesAsync();

        deleted.Should().Be(1);
        var remaining = await _context.TransactionStatuses.IgnoreQueryFilters().Select(s => s.Id).ToListAsync();
        remaining.Should().BeEquivalentTo([kept.Id, otherUser.Id, distinct.Id]);
        var translatedIds = await _context.EntityTranslations.Select(t => t.EntityId).ToListAsync();
        translatedIds.Should().BeEquivalentTo([kept.Id]);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static Transaction NewTransaction(Guid accountId, TransactionStatus status)
    {
        return new Transaction { Id = Guid.NewGuid(), UserId = UserId, BankAccountId = accountId, Label = "t", Amount = 1m, Date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Status = status };
    }

    private static TransactionCustomStatus NewStatus(string userId, string name, string? code, DateTime createdAtUtc)
    {
        return new TransactionCustomStatus { Id = Guid.NewGuid(), UserId = userId, Name = name, Code = code, CreatedAtUtc = createdAtUtc };
    }
}
