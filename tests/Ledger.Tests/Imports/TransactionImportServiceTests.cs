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

    private async Task<BankAccount> AddAccountAsync(string? ofxAccountId = null)
    {
        var bank = new Bank { UserId = "test-user-id", Name = "Banque Test" };
        _dbContext.Banks.Add(bank);
        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Test",
            OfxAccountId = ofxAccountId
        };
        _dbContext.BankAccounts.Add(account);
        await _dbContext.SaveChangesAsync();
        return account;
    }

    private static MemoryStream BuildOfxStream(string? accountId, params (string FitId, string Amount, string Name)[] lines)
    {
        var builder = new StringBuilder("<OFX>\n<BANKMSGSRSV1>\n<STMTTRNRS>\n<STMTRS>\n");
        if (accountId is not null)
        {
            builder.Append("<BANKACCTFROM>\n<ACCTID>").Append(accountId).Append("</ACCTID>\n</BANKACCTFROM>\n");
        }

        builder.Append("<BANKTRANLIST>\n");
        foreach (var (fitId, amount, name) in lines)
        {
            builder.Append("<STMTTRN>\n<DTPOSTED>20260301\n<TRNAMT>").Append(amount)
                .Append("\n<FITID>").Append(fitId).Append("\n<NAME>").Append(name).Append("\n</STMTTRN>\n");
        }

        builder.Append("</BANKTRANLIST>\n</STMTRS>\n</STMTTRNRS>\n</BANKMSGSRSV1>\n</OFX>");
        return new MemoryStream(Encoding.Latin1.GetBytes(builder.ToString()));
    }

    [Fact]
    public async Task ImportStatementAsync_WhenOfxAccountIdMatchesNoAccount_RequestsAccountSelection()
    {
        using var stream = BuildOfxStream("99999999", ("FIT-1", "-10.00", "BOULANGERIE"));

        var result = await _service.ImportStatementAsync(null, "releve.ofx", stream);

        result.Success.Should().BeFalse();
        result.RequiresAccountSelection.Should().BeTrue();
        result.DetectedOfxAcctId.Should().Be("99999999");
        _dbContext.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportStatementAsync_WhenOfxHasNoAccountIdAndNoSelection_RequestsAccountSelection()
    {
        using var stream = BuildOfxStream(null, ("FIT-1", "-10.00", "BOULANGERIE"));

        var result = await _service.ImportStatementAsync(null, "releve.ofx", stream);

        result.Success.Should().BeFalse();
        result.RequiresAccountSelection.Should().BeTrue();
        result.DetectedOfxAcctId.Should().BeNull();
    }

    [Fact]
    public async Task ImportStatementAsync_WhenAccountIsSelectedManually_ReportsTargetAccountWithoutOfxMatch()
    {
        var account = await AddAccountAsync();
        using var stream = BuildOfxStream(null, ("FIT-1", "-10.00", "BOULANGERIE"));

        var result = await _service.ImportStatementAsync(account.Id, "releve.ofx", stream);

        result.Success.Should().BeTrue();
        result.AutoMatchedByOfx.Should().BeFalse();
        result.TargetBankAccountId.Should().Be(account.Id);
        result.TargetBankAccountName.Should().Be("Banque Test - Compte Test");
        result.BatchId.Should().NotBeNull();
    }

    [Fact]
    public async Task ImportStatementAsync_WhenOfxAccountIdMatchesIgnoringSeparators_AutoMatchesAccount()
    {
        var account = await AddAccountAsync("1234-5678 901");
        using var stream = BuildOfxStream("12345678901", ("FIT-1", "-10.00", "BOULANGERIE"));

        var result = await _service.ImportStatementAsync(null, "releve.ofx", stream);

        result.AutoMatchedByOfx.Should().BeTrue();
        result.TargetBankAccountId.Should().Be(account.Id);
        result.TotalImported.Should().Be(1);
    }

    [Fact]
    public async Task ImportStatementAsync_WhenSimilarTransactionExistsWithOtherReference_ReportsSuspectedDuplicate()
    {
        var account = await AddAccountAsync();
        _dbContext.Transactions.Add(new Transaction
        {
            UserId = "test-user-id",
            BankAccountId = account.Id,
            Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 50.00m,
            Type = TransactionType.Debit,
            Label = "STATION ESSENCE",
            Reference = "ANCIENNE-REF"
        });
        await _dbContext.SaveChangesAsync();
        using var stream = BuildOfxStream(null, ("NOUVELLE-REF", "-50.00", "STATION ESSENCE"));

        var result = await _service.ImportStatementAsync(account.Id, "releve.ofx", stream);

        result.TotalImported.Should().Be(0);
        result.TotalDuplicatesSkipped.Should().Be(0);
        result.SuspectedDuplicates.Should().ContainSingle()
            .Which.ExistingMatchLabel.Should().Contain("Imports.ExistingMatchDesc");
    }

    [Fact]
    public async Task ImportStatementAsync_WhenSameOperationAppearsTwiceInFile_ReportsSecondAsSuspectedDuplicate()
    {
        var account = await AddAccountAsync();
        using var stream = BuildOfxStream(null, ("FIT-A", "-20.00", "CINEMA"), ("FIT-B", "-20.00", "CINEMA"));

        var result = await _service.ImportStatementAsync(account.Id, "releve.ofx", stream);

        result.TotalParsed.Should().Be(2);
        result.TotalImported.Should().Be(1);
        result.SuspectedDuplicates.Should().ContainSingle()
            .Which.ExistingMatchLabel.Should().Contain("Imports.BatchMatchDesc");
    }

    [Fact]
    public async Task ImportStatementAsync_WhenSameReferenceAppearsTwiceInFile_CountsSecondAsConfirmedDuplicate()
    {
        var account = await AddAccountAsync();
        using var stream = BuildOfxStream(null, ("FIT-A", "-20.00", "CINEMA"), ("FIT-A", "-35.00", "RESTAURANT"));

        var result = await _service.ImportStatementAsync(account.Id, "releve.ofx", stream);

        result.TotalImported.Should().Be(1);
        result.TotalDuplicatesSkipped.Should().Be(1);
        result.SuspectedDuplicates.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Pending", TransactionStatus.Pending)]
    [InlineData("Reconciled", TransactionStatus.Reconciled)]
    [InlineData("Autre", TransactionStatus.Cleared)]
    public async Task ImportStatementAsync_WhenDefaultCustomStatusExists_AppliesMappedStatus(string code, TransactionStatus expected)
    {
        var account = await AddAccountAsync();
        var status = new TransactionCustomStatus { UserId = "test-user-id", Name = "Statut", Code = code, IsDefault = true };
        _dbContext.TransactionStatuses.Add(status);
        await _dbContext.SaveChangesAsync();
        using var stream = BuildOfxStream(null, ("FIT-1", "-10.00", "BOULANGERIE"));

        await _service.ImportStatementAsync(account.Id, "releve.ofx", stream);

        var imported = _dbContext.Transactions.Single();
        imported.Status.Should().Be(expected);
        imported.CustomStatusId.Should().Be(status.Id);
    }

    [Fact]
    public async Task ImportStatementAsync_WhenParserFails_ReturnsFailureWithoutImporting()
    {
        var account = await AddAccountAsync();
        var parserMock = new Mock<IBankStatementParser>();
        parserMock.SetupGet(p => p.SupportedFormat).Returns("CSV");
        parserMock.Setup(p => p.CanHandle(It.IsAny<string>(), It.IsAny<Stream>())).Returns(true);
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CsvMappingProfile?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("fichier illisible"));
        var service = new TransactionImportService(
            new[] { parserMock.Object },
            _dbContext,
            _mockRulesEngine.Object,
            _mockUiSettings.Object,
            NullLogger<TransactionImportService>.Instance,
            _mockLocalizer.Object);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("x"));

        var result = await service.ImportStatementAsync(account.Id, "releve.csv", stream);

        result.Success.Should().BeFalse();
        result.BatchId.Should().BeNull();
        _dbContext.ImportBatches.Should().BeEmpty();
    }

    private async Task<BankAccount> AddAccountWithDebitsAsync(params (DateTime Date, string Label)[] debits)
    {
        var bank = new Bank { UserId = "test-user-id", Name = "LCL" };
        var account = new BankAccount { UserId = "test-user-id", BankId = bank.Id, Name = "Compte", Type = AccountType.Checking };
        _dbContext.Banks.Add(bank);
        _dbContext.BankAccounts.Add(account);
        foreach (var (date, label) in debits)
        {
            _dbContext.Transactions.Add(new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = date,
                Amount = 42m,
                Type = TransactionType.Debit,
                Label = label,
                RawLabel = label
            });
        }

        await _dbContext.SaveChangesAsync();
        return account;
    }

    [Fact]
    public async Task FindExistingDuplicatesAsync_WhenSameDayAndAmountButUnrelatedLabels_ReturnsNoGroup()
    {
        var day = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var account = await AddAccountWithDebitsAsync((day, "BOULANGERIE DUPONT"), (day, "GARAGE RENAULT LENS"));

        var duplicates = await _service.FindExistingDuplicatesAsync(account.Id);

        duplicates.Should().BeEmpty();
    }

    [Fact]
    public async Task FindExistingDuplicatesAsync_WhenSameLabelBeyondToleranceDays_ReturnsNoGroup()
    {
        var account = await AddAccountWithDebitsAsync(
            (new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), "ABONNEMENT FIBRE"),
            (new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc), "ABONNEMENT FIBRE"));

        var duplicates = await _service.FindExistingDuplicatesAsync(account.Id);

        duplicates.Should().BeEmpty();
    }

    [Fact]
    public async Task FindExistingDuplicatesAsync_WhenExactAndNearbyGroupsCoexist_ReturnsGroupsNewestFirstWithoutReusingTransactions()
    {
        var account = await AddAccountWithDebitsAsync(
            (new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), "RELAIS H GARE"),
            (new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), "RELAIS H GARE"),
            (new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc), "RELAIS H GARE"),
            (new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), "COTISATION ASSO"),
            (new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc), "COTISATION ASSO"));

        var duplicates = await _service.FindExistingDuplicatesAsync(account.Id);

        duplicates.Select(g => g.Transactions.Count).Should().Equal(2, 2);
        duplicates[0].Date.Should().Be(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc));
        duplicates[1].Date.Should().Be(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        duplicates.SelectMany(g => g.Transactions).Select(t => t.Id).Should().OnlyHaveUniqueItems();
    }
}
