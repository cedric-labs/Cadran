using System.Globalization;
using System.Text;
using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Imports.Entities;
using Ledger.Features.Imports.Services;
using Ledger.Features.Imports.Strategies;
using Ledger.Features.RulesEngine.Services;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ledger.Tests.Imports;

public sealed class TransactionImportServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly SqliteConnection _connection;
    private readonly Mock<IRulesEngineService> _mockRulesEngine;
    private readonly Mock<IUiSettingsService> _mockUiSettings;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly TransactionImportService _service;

    public TransactionImportServiceTests()
    {
        (_dbContext, _connection) = TestDbContextFactory.Create();

        _mockRulesEngine = new Mock<IRulesEngineService>();
        _mockUiSettings = new Mock<IUiSettingsService>();
        _mockUiSettings.Setup(u => u.GetDuplicateToleranceDaysAsync()).ReturnsAsync(2);

        _mockLocalizer = new Mock<IStringLocalizer>();
        _mockLocalizer.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns<string, object[]>((k, args) => new LocalizedString(k, string.Format(CultureInfo.CurrentCulture, k, args)));

        var parsers = new IBankStatementParser[]
        {
            new CsvStatementParser(),
            new OfxStatementParser()
        };

        _service = new TransactionImportService(
            parsers,
            _dbContext,
            _mockRulesEngine.Object,
            _mockUiSettings.Object,
            NullLogger<TransactionImportService>.Instance,
            _mockLocalizer.Object);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ImportStatementAsync_WhenNoParserMatches_ShouldReturnFailure()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("random binary content"));

        var result = await _service.ImportStatementAsync(null, "unsupported.bin", stream);

        result.Success.Should().BeFalse();
        result.TotalImported.Should().Be(0);
    }

    [Fact]
    public async Task ImportStatementAsync_WhenValidOfxWithMatchingAccount_ImportsSuccessfully()
    {
        // Arrange : création d'une banque et d'un compte avec OfxAccountId
        var bank = new Bank { UserId = "test-user-id", Name = "BoursoBank" };
        _dbContext.Banks.Add(bank);

        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Courant",
            Type = AccountType.Checking,
            OfxAccountId = "12345678901",
            InitialBalance = 1000m
        };
        _dbContext.BankAccounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var ofxContent = @"
OFXHEADER:100
<OFX>
<BANKMSGSRSV1>
<STMTTRNRS>
<STMTRS>
<BANKACCTFROM>
<ACCTID>12345678901</ACCTID>
</BANKACCTFROM>
<BANKTRANLIST>
<STMTTRN>
<TRNTYPE>DEBIT
<DTPOSTED>20260301
<TRNAMT>-32.50
<FITID>OFX-TX-001
<NAME>MONOPRIX
</STMTTRN>
</BANKTRANLIST>
</STMTRS>
</STMTTRNRS>
</BANKMSGSRSV1>
</OFX>";

        using var stream = new MemoryStream(Encoding.Latin1.GetBytes(ofxContent));

        // Act
        var result = await _service.ImportStatementAsync(null, "releve.ofx", stream);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalImported.Should().Be(1);
        result.TotalParsed.Should().Be(1);
        result.TotalDuplicatesSkipped.Should().Be(0);

        var tx = _dbContext.Transactions.FirstOrDefault(t => t.BankAccountId == account.Id);
        tx.Should().NotBeNull();
        tx.Amount.Should().Be(32.50m);
        tx.Label.Should().Be("MONOPRIX");
        tx.Reference.Should().Be("OFX-TX-001");
    }

    [Fact]
    public async Task ImportStatementAsync_WhenDuplicateReferenceExists_ShouldSkipDuplicate()
    {
        // Arrange
        var bank = new Bank { UserId = "test-user-id", Name = "BNP Paribas" };
        _dbContext.Banks.Add(bank);

        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Chèques",
            Type = AccountType.Checking,
            InitialBalance = 500m
        };
        _dbContext.BankAccounts.Add(account);

        // Transaction déjà présente avec la même référence FITID
        _dbContext.Transactions.Add(new Transaction
        {
            UserId = "test-user-id",
            BankAccountId = account.Id,
            Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 50.00m,
            Type = TransactionType.Debit,
            Label = "STATION ESSENCE",
            Reference = "FITID-DUPLICATE-999"
        });
        await _dbContext.SaveChangesAsync();

        var ofxContent = @"
<OFX>
<BANKTRANLIST>
<STMTTRN>
<DTPOSTED>20260301
<TRNAMT>-50.00
<FITID>FITID-DUPLICATE-999
<NAME>STATION ESSENCE
</STMTTRN>
</BANKTRANLIST>
</OFX>";

        using var stream = new MemoryStream(Encoding.Latin1.GetBytes(ofxContent));

        // Act
        var result = await _service.ImportStatementAsync(account.Id, "export.ofx", stream);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalImported.Should().Be(0);
        result.TotalDuplicatesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task FindExistingDuplicatesAsync_WithExactDateDuplicates_ShouldGroupThem()
    {
        var bank = new Bank { UserId = "test-user-id", Name = "LCL" };
        _dbContext.Banks.Add(bank);

        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Principal",
            Type = AccountType.Checking
        };
        _dbContext.BankAccounts.Add(account);

        var date = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.Transactions.AddRange(
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = date,
                Amount = 25.50m,
                Type = TransactionType.Debit,
                Label = "RELAIS H GARE",
                RawLabel = "CB RELAIS H GARE"
            },
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = date,
                Amount = 25.50m,
                Type = TransactionType.Debit,
                Label = "RELAIS H GARE",
                RawLabel = "CB RELAIS H GARE"
            }
        );
        await _dbContext.SaveChangesAsync();

        var duplicates = await _service.FindExistingDuplicatesAsync(account.Id);

        duplicates.Should().HaveCount(1);
        duplicates[0].Transactions.Should().HaveCount(2);
        duplicates[0].Amount.Should().Be(25.50m);
    }

    [Fact]
    public async Task FindExistingDuplicatesAsync_WithNearbyDateDuplicates_ShouldGroupThem()
    {
        var bank = new Bank { UserId = "test-user-id", Name = "LCL" };
        _dbContext.Banks.Add(bank);

        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Épargne",
            Type = AccountType.Savings
        };
        _dbContext.BankAccounts.Add(account);

        _dbContext.Transactions.AddRange(
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                Amount = 100.00m,
                Type = TransactionType.Debit,
                Label = "VIREMENT INTERNE",
                RawLabel = "VIR INTERNE"
            },
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), // 1 day diff (within tolerance)
                Amount = 100.00m,
                Type = TransactionType.Debit,
                Label = "VIREMENT INTERNE",
                RawLabel = "VIR INTERNE"
            }
        );
        await _dbContext.SaveChangesAsync();

        var duplicates = await _service.FindExistingDuplicatesAsync(account.Id);

        duplicates.Should().HaveCount(1);
        duplicates[0].Transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConfirmDuplicatesAsync_WhenInvoked_InsertsTransactionsAndAdjustsMetrics()
    {
        var bank = new Bank { UserId = "test-user-id", Name = "LCL" };
        _dbContext.Banks.Add(bank);

        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Courant",
            Type = AccountType.Checking
        };
        _dbContext.BankAccounts.Add(account);

        var batch = new ImportBatch
        {
            UserId = "test-user-id",
            BankAccountId = account.Id,
            FileName = "import.ofx",
            Format = "OFX",
            TotalParsed = 5,
            TotalImported = 3,
            TotalDuplicatesSkipped = 2,
            TotalAutoCategorized = 1
        };
        _dbContext.ImportBatches.Add(batch);
        await _dbContext.SaveChangesAsync();

        var suspects = new List<SuspectedDuplicateDto>
        {
            new(
                Index: 0,
                Date: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                ValueDate: null,
                Amount: 60.00m,
                Type: TransactionType.Debit,
                Label: "RESTAURANT LE COQ",
                RawLabel: null,
                ExistingMatchLabel: null
            )
        };

        var count = await _service.ConfirmDuplicatesAsync(batch.Id, account.Id, suspects);

        count.Should().Be(1);
        var inserted = _dbContext.Transactions.FirstOrDefault(t => t.Label == "RESTAURANT LE COQ");
        inserted.Should().NotBeNull();
        inserted.ImportBatchId.Should().Be(batch.Id);

        var updatedBatch = await _dbContext.ImportBatches.FindAsync(batch.Id);
        updatedBatch!.TotalImported.Should().Be(4);
        updatedBatch.TotalDuplicatesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task DeleteExistingDuplicatesAsync_WhenInvoked_RemovesSelectedTransactions()
    {
        var bank = new Bank { UserId = "test-user-id", Name = "LCL" };
        _dbContext.Banks.Add(bank);

        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Principal"
        };
        _dbContext.BankAccounts.Add(account);

        var tx1 = new Transaction
        {
            UserId = "test-user-id",
            BankAccountId = account.Id,
            Date = DateTime.Today,
            Amount = 15m,
            Type = TransactionType.Debit,
            Label = "DOUBLON A SUPPRIMER"
        };
        var tx2 = new Transaction
        {
            UserId = "test-user-id",
            BankAccountId = account.Id,
            Date = DateTime.Today,
            Amount = 15m,
            Type = TransactionType.Debit,
            Label = "ORIGINAL A GARDER"
        };
        _dbContext.Transactions.AddRange(tx1, tx2);
        await _dbContext.SaveChangesAsync();

        var deleted = await _service.DeleteExistingDuplicatesAsync(account.Id, new[] { tx1.Id });

        deleted.Should().Be(1);
        (await _dbContext.Transactions.FindAsync(tx1.Id)).Should().BeNull();
        (await _dbContext.Transactions.FindAsync(tx2.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteBatchAsync_WhenInvoked_DeletesBatchAndAssociatedTransactions()
    {
        var bank = new Bank { UserId = "test-user-id", Name = "LCL" };
        _dbContext.Banks.Add(bank);

        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Principal"
        };
        _dbContext.BankAccounts.Add(account);

        var batch = new ImportBatch
        {
            UserId = "test-user-id",
            BankAccountId = account.Id,
            FileName = "batch_to_delete.ofx",
            Format = "OFX"
        };
        _dbContext.ImportBatches.Add(batch);
        await _dbContext.SaveChangesAsync();

        var tx = new Transaction
        {
            UserId = "test-user-id",
            BankAccountId = account.Id,
            ImportBatchId = batch.Id,
            Date = DateTime.Today,
            Amount = 88m,
            Type = TransactionType.Debit,
            Label = "TX DU LOT"
        };
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var result = await _service.DeleteBatchAsync(batch.Id);

        result.Should().BeTrue();
        (await _dbContext.ImportBatches.FindAsync(batch.Id)).Should().BeNull();
        (await _dbContext.Transactions.FindAsync(tx.Id)).Should().BeNull();
    }

    [Fact]
    public async Task ImportStatementAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var bank = new Bank { UserId = "test-user-id", Name = "LCL" };
        _dbContext.Banks.Add(bank);
        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Courant"
        };
        _dbContext.BankAccounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var parserMock = new Mock<IBankStatementParser>();
        parserMock.SetupGet(p => p.SupportedFormat).Returns("CSV");
        parserMock.Setup(p => p.CanHandle(It.IsAny<string>(), It.IsAny<Stream>())).Returns(true);
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CsvMappingProfile?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = new TransactionImportService(
            new[] { parserMock.Object },
            _dbContext,
            _mockRulesEngine.Object,
            _mockUiSettings.Object,
            NullLogger<TransactionImportService>.Instance,
            _mockLocalizer.Object);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Date;Libelle;Montant"));
        var act = async () => await service.ImportStatementAsync(account.Id, "releve.csv", stream);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
