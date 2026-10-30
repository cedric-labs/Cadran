using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Reports.Models;
using Ledger.Features.Reports.Services;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Tests.Fixtures;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace Ledger.Tests.Reports;

public sealed class PivotReportServiceTests : IDisposable
{
    private readonly Ledger.Infrastructure.Data.AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Mock<IUiSettingsService> _mockUiSettings;
    private readonly PivotReportService _pivotService;
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _catHousingId = Guid.NewGuid();
    private readonly Guid _catFoodId = Guid.NewGuid();

    public PivotReportServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        var mockLocalizedService = new Mock<ILocalizedEntityService>();
        mockLocalizedService
            .Setup(s => s.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var mockLocalizer = new Mock<IStringLocalizer>();
        mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));

        _mockUiSettings = new Mock<IUiSettingsService>();
        _mockUiSettings.Setup(s => s.GetSalaryMonthStartDayAsync()).ReturnsAsync(1);

        _pivotService = new PivotReportService(_context, mockLocalizedService.Object, mockLocalizer.Object, _mockUiSettings.Object, TimeProvider.System);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque Test" };
        var account = new BankAccount
        {
            Id = _bankAccountId,
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Courant",
            InitialBalance = 1000m
        };

        var catHousing = new Category
        {
            Id = _catHousingId,
            UserId = "test-user-id",
            Name = "Logement",
            ColorHex = "#ef4444"
        };

        var catFood = new Category
        {
            Id = _catFoodId,
            UserId = "test-user-id",
            Name = "Alimentation",
            ColorHex = "#10b981"
        };

        var recurringRent = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Loyer",
            Pattern = "LOYER*",
            ExpectedAmount = 800m,
            TargetCategoryId = _catHousingId,
            IsActive = true
        };

        // Opérations 2026
        var transactions = new List<Transaction>
        {
            new() {
                Id = Guid.NewGuid(),
                UserId = "test-user-id",
                BankAccountId = _bankAccountId,
                Date = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
                Amount = 800m,
                Type = TransactionType.Debit,
                Label = "LOYER JANVIER",
                CategoryId = _catHousingId
            },
            new() {
                Id = Guid.NewGuid(),
                UserId = "test-user-id",
                BankAccountId = _bankAccountId,
                Date = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                Amount = 150m,
                Type = TransactionType.Debit,
                Label = "CARREFOUR MARKET",
                CategoryId = _catFoodId
            },
            new() {
                Id = Guid.NewGuid(),
                UserId = "test-user-id",
                BankAccountId = _bankAccountId,
                Date = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),
                Amount = 800m,
                Type = TransactionType.Debit,
                Label = "LOYER FEVRIER",
                CategoryId = _catHousingId
            },
            new() {
                Id = Guid.NewGuid(),
                UserId = "test-user-id",
                BankAccountId = _bankAccountId,
                Date = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
                Amount = 200m,
                Type = TransactionType.Debit,
                Label = "CARREFOUR DRIVE",
                CategoryId = _catFoodId
            },
            new() {
                Id = Guid.NewGuid(),
                UserId = "test-user-id",
                BankAccountId = _bankAccountId,
                Date = new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc),
                Amount = 2500m,
                Type = TransactionType.Credit,
                Label = "VIR SALAIRE SOCIETE",
                CategoryId = null
            }
        };

        _context.Banks.Add(bank);
        _context.BankAccounts.Add(account);
        _context.Categories.AddRange(catHousing, catFood);
        _context.RecurringExpenses.Add(recurringRent);
        _context.Transactions.AddRange(transactions);
        _context.SaveChanges();
    }

    [Fact]
    public async Task ComputePivotAsync_WhenDebitsOnlyByCategory_CalculatesAccurateSumsAndTotals()
    {
        // Arrange
        var query = new PivotQueryDto
        {
            FlowFilter = PivotFlowFilter.DebitsOnly,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            MetricType = PivotMetricType.Sum,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _pivotService.ComputePivotAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.TotalTransactionsCount.Should().Be(4); // 4 débits (salaire exclu)
        result.GrandTotal.Should().Be(1950m); // 800 + 150 + 800 + 200

        var housingRow = result.Rows.FirstOrDefault(r => string.Equals(r.Label, "Logement", StringComparison.Ordinal));
        housingRow.Should().NotBeNull();
        housingRow.RowTotal.Should().Be(1600m); // 800 (jan) + 800 (fev)
        housingRow.CellValues["2026-01"].Should().Be(800m);
        housingRow.CellValues["2026-02"].Should().Be(800m);

        var foodRow = result.Rows.FirstOrDefault(r => string.Equals(r.Label, "Alimentation", StringComparison.Ordinal));
        foodRow.Should().NotBeNull();
        foodRow.RowTotal.Should().Be(350m); // 150 + 200
    }

    [Fact]
    public async Task ComputePivot_RecurringOnly_ShouldFilterOnlyRecurringMatchingExpenses()
    {
        // Arrange
        var query = new PivotQueryDto
        {
            FlowFilter = PivotFlowFilter.RecurringOnly,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            MetricType = PivotMetricType.Sum,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _pivotService.ComputePivotAsync(query);

        // Assert
        result.TotalTransactionsCount.Should().Be(2); // Seuls les 2 loyers
        result.GrandTotal.Should().Be(1600m);
        result.Rows.Should().ContainSingle(r => r.Label == "Logement");
        result.Rows.Should().NotContain(r => r.Label == "Alimentation");
    }

    [Fact]
    public async Task ComputePivot_NonRecurringDebitsOnly_ShouldExcludeRecurringExpenses()
    {
        // Arrange
        var query = new PivotQueryDto
        {
            FlowFilter = PivotFlowFilter.NonRecurringDebitsOnly,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            MetricType = PivotMetricType.Sum,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _pivotService.ComputePivotAsync(query);

        // Assert
        result.TotalTransactionsCount.Should().Be(2); // Seuls les 2 achats Carrefour
        result.GrandTotal.Should().Be(350m);
        result.Rows.Should().ContainSingle(r => r.Label == "Alimentation");
        result.Rows.Should().NotContain(r => r.Label == "Logement");
    }

    [Fact]
    public async Task ComputePivot_CountMetric_ShouldCountOperationsAccurately()
    {
        // Arrange
        var query = new PivotQueryDto
        {
            FlowFilter = PivotFlowFilter.DebitsOnly,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            MetricType = PivotMetricType.Count,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = await _pivotService.ComputePivotAsync(query);

        // Assert
        var housingRow = result.Rows.First(r => string.Equals(r.Label, "Logement", StringComparison.Ordinal));
        housingRow.RowTotal.Should().Be(2); // 2 opérations
        housingRow.CellValues["2026-01"].Should().Be(1);
        housingRow.CellValues["2026-02"].Should().Be(1);
    }

    [Fact]
    public async Task SavedPivotReports_SaveAndGet_ShouldPersistSuccessfully()
    {
        // Arrange
        var query = new PivotQueryDto
        {
            FlowFilter = PivotFlowFilter.RecurringOnly,
            RowDimension = PivotRowDimension.Merchant
        };

        // Act
        var saved = await _pivotService.SavePivotReportAsync("Mon TCD Test", "Description", query, isDefault: true);
        var retrieved = await _pivotService.GetSavedPivotReportByIdAsync(saved.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Name.Should().Be("Mon TCD Test");
        retrieved.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task ComputePivot_WithShiftedBudgetDate_ShouldAggregateInShiftedMonthColumn()
    {
        // Transaction passée le 28 janvier 2026 mais avec BudgetDate au 1er février 2026
        var shiftedTx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc),
            BudgetDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 300m,
            Type = TransactionType.Debit,
            Label = "DEPENSE DECALEE",
            CategoryId = _catFoodId
        };
        _context.Transactions.Add(shiftedTx);
        await _context.SaveChangesAsync();

        var query = new PivotQueryDto
        {
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc),
            FlowFilter = PivotFlowFilter.DebitsOnly,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month
        };

        // Act
        var result = await _pivotService.ComputePivotAsync(query);

        // Assert : La dépense de 300€ doit se retrouver dans la colonne de février 2026 (BudgetDate) et pas janvier
        var foodRow = result.Rows.First(r => string.Equals(r.Label, "Alimentation", StringComparison.Ordinal));
        // Janvier = 150€ (du seed initial, l'opération décalée de 300€ en est exclue)
        foodRow.CellValues["2026-01"].Should().Be(150m);
        // Février = 200€ (du seed de février) + 300€ (décalé depuis janvier grâce à BudgetDate) = 500€
        foodRow.CellValues["2026-02"].Should().Be(500m);
    }

    [Fact]
    public async Task ComputePivot_ByCategory_ShouldIncludeLevel3MerchantSubRows()
    {
        // Arrange : Ajouter une sous-catégorie sous Alimentation et deux transactions de tiers différents
        var subCatSupermarket = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Supermarché",
            ParentCategoryId = _catFoodId
        };
        _context.Categories.Add(subCatSupermarket);

        var txAuchan = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            Amount = 80m,
            Type = TransactionType.Debit,
            Label = "AUCHAN VELIZY",
            CategoryId = subCatSupermarket.Id
        };
        var txCarrefour = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc),
            Amount = 45m,
            Type = TransactionType.Debit,
            Label = "CARREFOUR CITY",
            CategoryId = subCatSupermarket.Id
        };
        _context.Transactions.AddRange(txAuchan, txCarrefour);
        await _context.SaveChangesAsync();

        var query = new PivotQueryDto
        {
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            FlowFilter = PivotFlowFilter.DebitsOnly,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month
        };

        // Act
        var result = await _pivotService.ComputePivotAsync(query);

        // Assert
        var foodRow = result.Rows.First(r => string.Equals(r.Label, "Alimentation", StringComparison.Ordinal));
        foodRow.SubRows.Should().NotBeEmpty();

        var supermarketSubRow = foodRow.SubRows.First(s => string.Equals(s.Label, "Supermarché", StringComparison.Ordinal));
        supermarketSubRow.SubRows.Should().NotBeEmpty(); // Niveau 3 tiers/marchands
        supermarketSubRow.SubRows.Should().Contain(m => m.Label.Contains("AUCHAN"));
        supermarketSubRow.SubRows.Should().Contain(m => m.Label.Contains("CARREFOUR"));

        var auchanLevel3 = supermarketSubRow.SubRows.First(m => m.Label.Contains("AUCHAN", StringComparison.Ordinal));
        auchanLevel3.CellValues["2026-01"].Should().Be(80m);
    }

    [Fact]
    public async Task GetCellDetailAsync_WhenCellSelected_ReturnsTransactionsForSpecificCell()
    {
        // Arrange
        var query = new PivotQueryDto
        {
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            FlowFilter = PivotFlowFilter.DebitsOnly,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month
        };

        // Act : Récupérer le détail pour Alimentation (_catFoodId) sur le mois "2026-01"
        var detail = await _pivotService.GetCellDetailAsync(query, _catFoodId.ToString(), "2026-01");

        // Assert
        detail.Should().NotBeNull();
        detail.ColumnLabel.Should().Contain("2026");
        detail.Transactions.Should().NotBeEmpty();
        detail.Transactions.Should().OnlyContain(t => t.CategoryName == "Alimentation" || t.Amount == 150m);
        detail.TotalAmount.Should().Be(-150m);
    }

    [Fact]
    public async Task ComputePivotAsync_WithMerchantAssigned_ShouldAggregateUnderMerchantName()
    {
        // Arrange
        var merchant = new Ledger.Features.Merchants.Entities.Merchant
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Lidl",
            NormalizedName = "lidl"
        };
        _context.Merchants.Add(merchant);

        var tx1 = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            CategoryId = _catFoodId,
            MerchantId = merchant.Id,
            Date = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            Label = "PAIEMENT CB LIDL LENS 62",
            Amount = 40m,
            Type = TransactionType.Debit
        };

        var tx2 = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            CategoryId = _catFoodId,
            MerchantId = merchant.Id,
            Date = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Label = "PAIEMENT CARTE LIDL VENDIN LE VIEIL",
            Amount = 60m,
            Type = TransactionType.Debit
        };

        _context.Transactions.AddRange(tx1, tx2);
        await _context.SaveChangesAsync();

        var query = new PivotQueryDto
        {
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            FlowFilter = PivotFlowFilter.DebitsOnly,
            RowDimension = PivotRowDimension.Merchant,
            ColumnDimension = PivotColumnDimension.Month
        };

        // Act
        var result = await _pivotService.ComputePivotAsync(query);

        // Assert
        result.Rows.Should().ContainSingle(r => r.Label == "Lidl");
        var lidlRow = result.Rows.First(r => string.Equals(r.Label, "Lidl", StringComparison.Ordinal));
        lidlRow.CellValues["2026-01"].Should().Be(100m);
    }

    [Fact]
    public async Task ComputePivot_SplitTransactions_ShouldAggregateChildSplitsAccurately()
    {
        // Arrange
        var splitTx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            Amount = 100m,
            Type = TransactionType.Debit,
            Label = "SUPERMARCHE & BRICOLAGE",
            IsSplit = true
        };

        var split1 = new TransactionSplit
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            TransactionId = splitTx.Id,
            Amount = 60m,
            CategoryId = _catFoodId,
            Notes = "Alimentation"
        };

        var split2 = new TransactionSplit
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            TransactionId = splitTx.Id,
            Amount = 40m,
            CategoryId = _catHousingId,
            Notes = "Bricolage maison"
        };

        splitTx.Splits.Add(split1);
        splitTx.Splits.Add(split2);

        _context.Transactions.Add(splitTx);
        await _context.SaveChangesAsync();

        var query = new PivotQueryDto
        {
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc),
            FlowFilter = PivotFlowFilter.DebitsOnly,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month
        };

        // Act
        var result = await _pivotService.ComputePivotAsync(query);

        // Assert
        var foodRow = result.Rows.FirstOrDefault(r => string.Equals(r.RowKey, _catFoodId.ToString(), StringComparison.Ordinal));
        var housingRow = result.Rows.FirstOrDefault(r => string.Equals(r.RowKey, _catHousingId.ToString(), StringComparison.Ordinal));

        foodRow.Should().NotBeNull();
        foodRow.CellValues["2026-03"].Should().Be(60m);

        housingRow.Should().NotBeNull();
        housingRow.CellValues["2026-03"].Should().Be(40m);

        result.GrandTotal.Should().Be(100m);
    }

    [Fact]
    public async Task ComputePivot_DirectionalSignsAndAnomalies_ShouldDetectReverseFlow()
    {
        // Arrange : création d'une catégorie de revenu
        var catSalary = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Salaire",
            IsIncome = true,
            ColorHex = "#22c55e"
        };
        _context.Categories.Add(catSalary);

        // Opération de remboursement (crédit) dans une catégorie de dépense (Alimentation)
        var refundTx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            Amount = 30m,
            Type = TransactionType.Credit,
            Label = "REMBOURSEMENT CARREFOUR",
            CategoryId = _catFoodId
        };

        // Opération de débit dans une catégorie de revenu
        var expenseOnIncomeTx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            Amount = 50m,
            Type = TransactionType.Debit,
            Label = "REGULARISATION SALAIRE TROP-PERCU",
            CategoryId = catSalary.Id
        };

        _context.Transactions.AddRange(refundTx, expenseOnIncomeTx);
        await _context.SaveChangesAsync();

        var query = new PivotQueryDto
        {
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            FlowFilter = PivotFlowFilter.All,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month
        };

        // Act
        var result = await _pivotService.ComputePivotAsync(query);

        // Assert
        var foodRow = result.Rows.FirstOrDefault(r => string.Equals(r.RowKey, _catFoodId.ToString(), StringComparison.Ordinal));
        var salaryRow = result.Rows.FirstOrDefault(r => string.Equals(r.RowKey, catSalary.Id.ToString(), StringComparison.Ordinal));

        foodRow.Should().NotBeNull();
        foodRow.CellHasAnomalies["2026-04"].Should().BeTrue();
        foodRow.CellValues["2026-04"].Should().Be(-30m); // Inversé / déduction

        salaryRow.Should().NotBeNull();
        salaryRow.CellHasAnomalies["2026-04"].Should().BeTrue();
        salaryRow.CellValues["2026-04"].Should().Be(-50m);
    }

    [Fact]
    public async Task ComputePivot_SalaryMonth_ShouldApplyCustomDayBounds()
    {
        // Arrange
        _mockUiSettings.Setup(s => s.GetSalaryMonthStartDayAsync()).ReturnsAsync(25);

        var query = new PivotQueryDto
        {
            PeriodPreset = PivotPeriodPreset.CurrentMonth,
            FlowFilter = PivotFlowFilter.DebitsOnly,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month
        };

        // Act
        var result = await _pivotService.ComputePivotAsync(query);

        // Assert : la date de début doit être un 25 et la date de fin un 24
        result.EffectiveStartDate.Day.Should().Be(25);
        result.EffectiveEndDate.Day.Should().Be(24);
    }

    [Fact]
    public async Task GetCellDetailAsync_WithSplitTransactions_ShouldReturnIndividualSplitParts()
    {
        // Arrange
        var splitTx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc),
            Amount = 150m,
            Type = TransactionType.Debit,
            Label = "ACHAT GLOBAL",
            IsSplit = true
        };

        var splitPart = new TransactionSplit
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            TransactionId = splitTx.Id,
            Amount = 70m,
            CategoryId = _catFoodId,
            Notes = "Courses épicerie"
        };

        var splitPart2 = new TransactionSplit
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            TransactionId = splitTx.Id,
            Amount = 80m,
            CategoryId = _catHousingId,
            Notes = "Fournitures bureau"
        };

        splitTx.Splits.Add(splitPart);
        splitTx.Splits.Add(splitPart2);
        _context.Transactions.Add(splitTx);
        await _context.SaveChangesAsync();

        var query = new PivotQueryDto
        {
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            FlowFilter = PivotFlowFilter.DebitsOnly,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month
        };

        // Act
        var detail = await _pivotService.GetCellDetailAsync(query, _catFoodId.ToString(), "2026-05");

        // Assert
        detail.Transactions.Should().ContainSingle(t => t.Id == splitPart.Id);
        var item = detail.Transactions.First(t => t.Id == splitPart.Id);
        item.Amount.Should().Be(-70m);
        item.CategoryName.Should().Be("Alimentation");
    }

    [Fact]
    public async Task SavedPivotReports_CrudOperations_ShouldWorkCorrectly()
    {
        var query = new PivotQueryDto
        {
            PeriodPreset = PivotPeriodPreset.CurrentMonth,
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month
        };

        // 1. Create
        var saved = await _pivotService.SavePivotReportAsync("Mon Rapport Mensuel", "Description du rapport", query, isDefault: true);
        saved.Should().NotBeNull();
        saved.Name.Should().Be("Mon Rapport Mensuel");
        saved.IsDefault.Should().BeTrue();

        // 2. Read list & by ID
        var list = await _pivotService.GetSavedPivotReportsAsync();
        list.Should().ContainSingle(r => r.Id == saved.Id);

        var byId = await _pivotService.GetSavedPivotReportByIdAsync(saved.Id);
        byId.Should().NotBeNull();
        byId.Name.Should().Be("Mon Rapport Mensuel");

        // 3. Update existing
        var updated = await _pivotService.SavePivotReportAsync("Mon Rapport Renommé", "Nouvelle desc", query, existingId: saved.Id);
        updated.Id.Should().Be(saved.Id);
        updated.Name.Should().Be("Mon Rapport Renommé");

        // 4. Delete
        var deleted = await _pivotService.DeleteSavedPivotReportAsync(saved.Id);
        deleted.Should().BeTrue();

        var afterDelete = await _pivotService.GetSavedPivotReportByIdAsync(saved.Id);
        afterDelete.Should().BeNull();
    }

    [Fact]
    public async Task GetCellDetailAsync_WithSpecificCategoryAndMonth_ShouldReturnMatchingTransactions()
    {
        var query = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            FlowFilter = PivotFlowFilter.All
        };

        var detail = await _pivotService.GetCellDetailAsync(query, _catHousingId.ToString(), "2026-01");

        detail.Should().NotBeNull();
        detail.TransactionsCount.Should().Be(1);
        detail.TotalAmount.Should().Be(-800m);
        detail.RowLabel.Should().Be("Logement");
        detail.Transactions.Should().ContainSingle(t => t.Label == "LOYER JANVIER");
    }

    [Fact]
    public async Task GetCellDetailAsync_WithTotalColumn_ShouldAggregateAllMonths()
    {
        var query = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            FlowFilter = PivotFlowFilter.All
        };

        var detail = await _pivotService.GetCellDetailAsync(query, _catHousingId.ToString(), "TOTAL");

        detail.Should().NotBeNull();
        detail.ColumnLabel.Should().Be("Total");
        detail.TransactionsCount.Should().BeGreaterThanOrEqualTo(2);
        detail.TotalAmount.Should().BeLessThanOrEqualTo(-1600m);
    }

    [Fact]
    public async Task GetCellDetailAsync_WithQuarterColumn_ShouldFilterQuarter()
    {
        var query = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Quarter,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            FlowFilter = PivotFlowFilter.All
        };

        var detail = await _pivotService.GetCellDetailAsync(query, _catHousingId.ToString(), "2026-Q1");

        detail.Should().NotBeNull();
        detail.ColumnLabel.Should().Be("T1 2026");
        detail.TransactionsCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetCellDetailAsync_WithMerchantRowUnderCategory_FiltersMerchantAndCategory()
    {
        var merchant = new Merchant
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Loyer Bailleur",
            NormalizedName = "loyer bailleur"
        };
        _context.Merchants.Add(merchant);

        var rentTx = _context.Transactions.First(t => t.Label == "LOYER JANVIER");
        rentTx.MerchantId = merchant.Id;
        await _context.SaveChangesAsync();

        var query = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var rowKey = $"{_catHousingId}__m__Loyer Bailleur";
        var detail = await _pivotService.GetCellDetailAsync(query, rowKey, "2026-01");

        detail.Should().NotBeNull();
        detail.RowLabel.Should().Be("Loyer Bailleur");
        detail.TransactionsCount.Should().Be(1);
    }

    [Fact]
    public async Task GetCellDetailAsync_WithDirectCategoryAndUncategorizedRows_FiltersProperly()
    {
        // 1. Transaction non catégorisée
        var uncatTx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            Amount = 45m,
            Type = TransactionType.Debit,
            Label = "ACHAT SANS CATEGORIE",
            CategoryId = null
        };
        _context.Transactions.Add(uncatTx);
        await _context.SaveChangesAsync();

        var query = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        // Test "uncategorized"
        var uncatDetail = await _pivotService.GetCellDetailAsync(query, "uncategorized", "2026-01");
        uncatDetail.Should().NotBeNull();
        uncatDetail.Transactions.Should().Contain(t => t.Label == "ACHAT SANS CATEGORIE");

        // Test "uncategorized__m__ACHAT"
        var uncatMerchantDetail = await _pivotService.GetCellDetailAsync(query, "uncategorized__m__ACHAT", "2026-01");
        uncatMerchantDetail.Should().NotBeNull();

        // Test "{catId}__direct"
        var directDetail = await _pivotService.GetCellDetailAsync(query, $"{_catHousingId}__direct", "2026-01");
        directDetail.Should().NotBeNull();

        // Test "{catId}__direct__m__Loyer Bailleur"
        var directMerchantDetail = await _pivotService.GetCellDetailAsync(query, $"{_catHousingId}__direct__m__Loyer Bailleur", "2026-01");
        directMerchantDetail.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCellDetailAsync_WithAccountAndMerchantRowDimensions_FiltersProperly()
    {
        var queryAccount = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Account,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var accDetail = await _pivotService.GetCellDetailAsync(queryAccount, _bankAccountId.ToString(), "2026-01");
        accDetail.Should().NotBeNull();
        accDetail.TransactionsCount.Should().BeGreaterThanOrEqualTo(1);

        var queryMerchant = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Merchant,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var merchantDetail = await _pivotService.GetCellDetailAsync(queryMerchant, "Carrefour", "2026-01");
        merchantDetail.Should().NotBeNull();
        merchantDetail.RowLabel.Should().Be("Carrefour");
    }

    [Fact]
    public async Task GetCellDetailAsync_WithQuarterAndYearDimensions_FiltersColumnsProperly()
    {
        var queryQuarter = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Quarter,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var qDetail = await _pivotService.GetCellDetailAsync(queryQuarter, _catHousingId.ToString(), "2026-Q1");
        qDetail.Should().NotBeNull();
        qDetail.ColumnLabel.Should().Be("T1 2026");

        var queryYear = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Year,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var yearDetail = await _pivotService.GetCellDetailAsync(queryYear, _catHousingId.ToString(), "2026");
        yearDetail.Should().NotBeNull();
        yearDetail.ColumnLabel.Should().Be("2026");

        // Unrecognized colKey and unknown rowKey fallback
        var unknownDetail = await _pivotService.GetCellDetailAsync(queryYear, "not-a-valid-guid", "unknown-column");
        unknownDetail.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCellDetailAsync_WithSelectedAccountsAndCategories_AppliesPreFilters()
    {
        var query = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            SelectedAccountIds = new List<Guid> { _bankAccountId },
            SelectedCategoryIds = new List<Guid> { _catHousingId }
        };

        var detail = await _pivotService.GetCellDetailAsync(query, _catHousingId.ToString(), "2026-01");
        detail.Should().NotBeNull();
        detail.TransactionsCount.Should().Be(1);
    }

    [Fact]
    public async Task SavedPivotReport_CrudOperationsAndDefaultSwitching_WorkCorrectly()
    {
        // 1. Sauvegarder un 1er rapport par défaut
        var queryCat = new PivotQueryDto { RowDimension = PivotRowDimension.Category };
        var r1 = await _pivotService.SavePivotReportAsync("Rapport 1", "Description 1", queryCat, null, isDefault: true);
        r1.Should().NotBeNull();
        r1.IsDefault.Should().BeTrue();

        // 2. Sauvegarder un 2ème rapport par défaut (doit basculer le 1er en IsDefault = false)
        var queryAcc = new PivotQueryDto { RowDimension = PivotRowDimension.Account };
        var r2 = await _pivotService.SavePivotReportAsync("Rapport 2", null, queryAcc, null, isDefault: true);
        r2.Should().NotBeNull();
        r2.IsDefault.Should().BeTrue();

        var list = await _pivotService.GetSavedPivotReportsAsync();
        list.Should().HaveCount(2);

        var r1Reloaded = await _pivotService.GetSavedPivotReportByIdAsync(r1.Id);
        r1Reloaded!.IsDefault.Should().BeFalse();

        // 3. Mettre à jour r1
        var r1Updated = await _pivotService.SavePivotReportAsync("Rapport 1 Modifié", "Nouvelle desc", queryCat, r1.Id, false);
        r1Updated.Id.Should().Be(r1.Id);
        r1Updated.Name.Should().Be("Rapport 1 Modifié");

        // 4. Tenter de mettre à jour un ID inexistant (doit créer une nouvelle entrée)
        var nonExistentId = Guid.NewGuid();
        var rNew = await _pivotService.SavePivotReportAsync("Rapport Orphelin", null, queryCat, nonExistentId, false);
        rNew.Should().NotBeNull();
        rNew.Id.Should().NotBe(nonExistentId);

        // 5. Suppression
        var deleteSuccess = await _pivotService.DeleteSavedPivotReportAsync(r1.Id);
        deleteSuccess.Should().BeTrue();

        var deleteNonExistent = await _pivotService.DeleteSavedPivotReportAsync(Guid.NewGuid());
        deleteNonExistent.Should().BeFalse();
    }

    [Fact]
    public async Task ComputePivotAsync_WithPeriodPresetsAndSearchAndChildCategories_CoversBranches()
    {
        // 1. Période Custom inversée (start > end, doit être inversée automatiquement)
        var queryInverted = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var resInverted = await _pivotService.ComputePivotAsync(queryInverted);
        resInverted.Should().NotBeNull();

        // 2. Période Last12Months et PreviousYear
        var query12M = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Last12Months
        };
        var res12M = await _pivotService.ComputePivotAsync(query12M);
        res12M.Should().NotBeNull();

        var queryPrevYear = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.PreviousYear
        };
        var resPrevYear = await _pivotService.ComputePivotAsync(queryPrevYear);
        resPrevYear.Should().NotBeNull();

        // 3. Recherche par libellé et FlowFilter CreditsOnly
        var querySearch = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            SearchTerm = "LOYER",
            FlowFilter = PivotFlowFilter.CreditsOnly
        };
        var resSearch = await _pivotService.ComputePivotAsync(querySearch);
        resSearch.Should().NotBeNull();

        // 4. Sélection de catégorie parente avec sous-catégories
        var queryParentCat = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            SelectedCategoryIds = new List<Guid> { _catHousingId } // Housing est le parent de Rent
        };
        var resParentCat = await _pivotService.ComputePivotAsync(queryParentCat);
        resParentCat.Should().NotBeNull();
        resParentCat.Rows.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ComputePivot_AverageMetric_ShouldComputeAveragePerPeriod()
    {
        var query = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            MetricType = PivotMetricType.Average,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = await _pivotService.ComputePivotAsync(query);

        result.Should().NotBeNull();
        result.Rows.Should().NotBeEmpty();
        var rentRow = result.Rows.FirstOrDefault(r => string.Equals(r.Label, "Logement", StringComparison.Ordinal));
        rentRow.Should().NotBeNull();
        rentRow.MonthlyAverage.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ComputePivot_QuarterAndYearDimensions_ShouldAggregateCorrectly()
    {
        var queryQuarter = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Quarter,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var resQuarter = await _pivotService.ComputePivotAsync(queryQuarter);
        resQuarter.Columns.Should().Contain(c => c.Key == "2026-Q1");

        var queryYear = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Year,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var resYear = await _pivotService.ComputePivotAsync(queryYear);
        resYear.Columns.Should().Contain(c => c.Key == "2026");
    }

    [Fact]
    public async Task ComputePivot_AccountAndMerchantRowDimensions_ShouldGroupCorrectly()
    {
        var queryAccount = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Account,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var resAccount = await _pivotService.ComputePivotAsync(queryAccount);
        resAccount.Rows.Should().Contain(r => r.Label == "Compte Courant");

        var queryMerchant = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Merchant,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var resMerchant = await _pivotService.ComputePivotAsync(queryMerchant);
        resMerchant.Rows.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ComputePivot_WhenNoTransactionsMatch_ReturnsEmptyRowsAndZeroTotal()
    {
        var query = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.Custom,
            StartDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2020, 1, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = await _pivotService.ComputePivotAsync(query);

        result.Rows.Should().BeEmpty();
        result.GrandTotal.Should().Be(0);
    }

    [Fact]
    public async Task SavedPivotReport_NotFoundOperations_ReturnExpectedNullOrFalse()
    {
        var nonExistentId = Guid.NewGuid();

        var report = await _pivotService.GetSavedPivotReportByIdAsync(nonExistentId);
        report.Should().BeNull();

        var deleted = await _pivotService.DeleteSavedPivotReportAsync(nonExistentId);
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task ComputePivotAsync_WhenTodayIsFixed_ResolvesCurrentMonthFromTimeProvider()
    {
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2025, 4, 18, 9, 0, 0, TimeSpan.Zero));
        var mockLocalizedService = new Mock<ILocalizedEntityService>();
        mockLocalizedService
            .Setup(s => s.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        var mockLocalizer = new Mock<IStringLocalizer>();
        mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));

        var service = new PivotReportService(_context, mockLocalizedService.Object, mockLocalizer.Object, _mockUiSettings.Object, fakeTime);

        var query = new PivotQueryDto
        {
            RowDimension = PivotRowDimension.Category,
            ColumnDimension = PivotColumnDimension.Month,
            PeriodPreset = PivotPeriodPreset.CurrentMonth
        };

        var result = await service.ComputePivotAsync(query);

        result.EffectiveStartDate.Should().Be(new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        result.EffectiveEndDate.Should().Be(new DateTime(2025, 4, 30, 0, 0, 0, DateTimeKind.Utc));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
