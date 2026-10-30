using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Reports.Models;
using Ledger.Features.Reports.Services;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace Ledger.Tests.Reports;

public class PivotAggregationCalculatorTests
{
    private readonly PivotAggregationCalculator _calculator;

    public PivotAggregationCalculatorTests()
    {
        var mockLocalizer = new Mock<IStringLocalizer>();
        mockLocalizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _calculator = new PivotAggregationCalculator(mockLocalizer.Object);
    }

    [Fact]
    public void BuildColumnHeaders_WhenMonthDimension_ReturnsOrderedMonthlyHeaders()
    {
        // Arrange
        var start = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var columns = _calculator.BuildColumnHeaders(start, end, PivotColumnDimension.Month);

        // Assert
        columns.Should().HaveCount(3);
        columns[0].Key.Should().Be("2026-01");
        columns[1].Key.Should().Be("2026-02");
        columns[2].Key.Should().Be("2026-03");
    }

    [Fact]
    public void BuildColumnHeaders_WhenQuarterDimension_ReturnsQuarterlyHeaders()
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var columns = _calculator.BuildColumnHeaders(start, end, PivotColumnDimension.Quarter);

        // Assert
        columns.Should().HaveCount(2);
        columns[0].Key.Should().Be("2026-Q1");
        columns[1].Key.Should().Be("2026-Q2");
    }

    [Fact]
    public void BuildColumnHeaders_WhenYearDimension_ReturnsYearlyHeaders()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var columns = _calculator.BuildColumnHeaders(start, end, PivotColumnDimension.Year);

        // Assert
        columns.Should().HaveCount(3);
        columns[0].Key.Should().Be("2024");
        columns[1].Key.Should().Be("2025");
        columns[2].Key.Should().Be("2026");
    }

    [Fact]
    public void GetColumnKeyForDate_WhenGivenDate_ReturnsFormattedKeyForDimension()
    {
        // Arrange
        var date = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        _calculator.GetColumnKeyForDate(date, PivotColumnDimension.Month).Should().Be("2026-04");
        _calculator.GetColumnKeyForDate(date, PivotColumnDimension.Quarter).Should().Be("2026-Q2");
        _calculator.GetColumnKeyForDate(date, PivotColumnDimension.Year).Should().Be("2026");
    }

    [Fact]
    public void CalculateTotalsAndPercentages_WhenRowsProvided_ComputesGrandTotalAndPercentages()
    {
        // Arrange
        var col1 = new PivotColumnHeaderDto { Key = "2026-01", Label = "Jan 2026", SortOrder = 1 };
        var col2 = new PivotColumnHeaderDto { Key = "2026-02", Label = "Fév 2026", SortOrder = 2 };

        var row1 = new PivotRowDto
        {
            RowKey = "row-1",
            Label = "Ligne 1",
            RowTotal = 100m,
            CellValues = new Dictionary<string, decimal>(StringComparer.Ordinal) { { "2026-01", 60m }, { "2026-02", 40m } }
        };

        var row2 = new PivotRowDto
        {
            RowKey = "row-2",
            Label = "Ligne 2",
            RowTotal = 300m,
            CellValues = new Dictionary<string, decimal>(StringComparer.Ordinal) { { "2026-01", 140m }, { "2026-02", 160m } }
        };

        var result = new PivotResultDto
        {
            Columns = new List<PivotColumnHeaderDto> { col1, col2 },
            Rows = new List<PivotRowDto> { row1, row2 }
        };

        // Act
        _calculator.CalculateTotalsAndPercentages(result);

        // Assert
        result.GrandTotal.Should().Be(400m);
        result.ColumnTotals["2026-01"].Should().Be(200m);
        result.ColumnTotals["2026-02"].Should().Be(200m);
        row1.PercentOfGrandTotal.Should().Be(25.0m);
        row2.PercentOfGrandTotal.Should().Be(75.0m);
    }

    private static readonly DateTime January = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

    private static PivotTransactionItem Tx(Guid? categoryId, decimal amount, TransactionType type, string label, DateTime? date = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Date = date ?? January,
            Amount = amount,
            Type = type,
            CategoryId = categoryId,
            Label = label
        };

    private List<PivotRowDto> AggregateCategories(
        IReadOnlyList<PivotTransactionItem> transactions,
        IReadOnlyList<Category> categories,
        PivotMetricType metric = PivotMetricType.Sum)
    {
        var columns = _calculator.BuildColumnHeaders(January, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), PivotColumnDimension.Month);
        var query = new PivotQueryDto { RowDimension = PivotRowDimension.Category, MetricType = metric };
        return _calculator.AggregateRows(transactions, columns, categories, [], new Dictionary<Guid, string>(), query);
    }

    [Fact]
    public void AggregateRows_WhenSubCategoryTransactions_BuildsCategoryMerchantHierarchy()
    {
        // Arrange
        var parent = new Category { UserId = "u", Name = "Maison", IconName = "bi-house", ColorHex = "#111111" };
        var child = new Category { UserId = "u", Name = "Loyer", ParentCategoryId = parent.Id, IconName = "bi-key", ColorHex = "#222222" };
        var transactions = new[]
        {
            Tx(child.Id, 100m, TransactionType.Debit, "AGENCE"),
            Tx(child.Id, 30m, TransactionType.Debit, "AGENCE"),
            Tx(child.Id, 20m, TransactionType.Credit, "AGENCE")
        };

        // Act
        var rows = AggregateCategories(transactions, [parent, child]);

        // Assert
        var top = rows.Should().ContainSingle().Subject;
        top.Label.Should().Be("Maison");
        top.RowTotal.Should().Be(110m);
        top.CellValues["2026-01"].Should().Be(110m);
        top.CellCounts["2026-01"].Should().Be(3);
        top.CellHasAnomalies["2026-01"].Should().BeTrue();
        top.CellHasAnomalies["2026-02"].Should().BeFalse();
        top.CellIsCredit["2026-01"].Should().BeFalse();
        top.MonthlyAverage.Should().Be(55m);
        var sub = top.SubRows.Should().ContainSingle().Subject;
        sub.Label.Should().Be("Loyer");
        sub.IsSubRow.Should().BeTrue();
        sub.ParentRowKey.Should().Be(parent.Id.ToString());
        sub.IconName.Should().Be("bi-key");
        sub.RowTotal.Should().Be(110m);
        var merchant = sub.SubRows.Should().ContainSingle().Subject;
        merchant.IconName.Should().Be("bi-shop");
        merchant.ColorHex.Should().Be("#222222");
        merchant.ParentRowKey.Should().Be(child.Id.ToString());
        merchant.RowTotal.Should().Be(110m);
    }

    [Fact]
    public void AggregateRows_WhenTransactionHasNoCategory_GroupsUnderUncategorizedRow()
    {
        // Arrange
        var transactions = new[] { Tx(null, 40m, TransactionType.Debit, "BOULANGERIE") };

        // Act
        var rows = AggregateCategories(transactions, []);

        // Assert
        var row = rows.Should().ContainSingle().Subject;
        row.RowKey.Should().Be("uncategorized");
        row.Label.Should().Be("Common.Uncategorized");
        row.RowTotal.Should().Be(40m);
        var merchant = row.SubRows.Should().ContainSingle().Subject;
        merchant.ParentRowKey.Should().Be("uncategorized");
        merchant.ColorHex.Should().Be("#94a3b8");
    }

    [Fact]
    public void AggregateRows_WhenTransactionOnParentWithChildren_UsesDirectGeneralSubRow()
    {
        // Arrange
        var parent = new Category { UserId = "u", Name = "Loisirs", IsIncome = false };
        var child = new Category { UserId = "u", Name = "Cinéma", ParentCategoryId = parent.Id };
        var transactions = new[] { Tx(parent.Id, 15m, TransactionType.Debit, "CINEMA") };

        // Act
        var rows = AggregateCategories(transactions, [parent, child]);

        // Assert
        var sub = rows.Should().ContainSingle().Subject.SubRows.Should().ContainSingle().Subject;
        sub.RowKey.Should().Be($"{parent.Id}__direct");
        sub.Label.Should().Be("Loisirs (Common.General)");
        sub.SubRows.Should().ContainSingle();
    }

    [Fact]
    public void AggregateRows_WhenTransactionOnLeafTopCategory_AttachesMerchantDirectlyToParent()
    {
        // Arrange
        var salary = new Category { UserId = "u", Name = "Salaire", IsIncome = true, ColorHex = "#333333" };
        var transactions = new[] { Tx(salary.Id, 2000m, TransactionType.Credit, "EMPLOYEUR") };

        // Act
        var rows = AggregateCategories(transactions, [salary]);

        // Assert
        var top = rows.Should().ContainSingle().Subject;
        top.IsIncome.Should().BeTrue();
        top.CellIsCredit["2026-01"].Should().BeTrue();
        top.CellHasAnomalies["2026-01"].Should().BeFalse();
        var merchant = top.SubRows.Should().ContainSingle().Subject;
        merchant.ParentRowKey.Should().Be(salary.Id.ToString());
        merchant.ColorHex.Should().Be("#333333");
        merchant.IsIncome.Should().BeTrue();
    }

    [Fact]
    public void AggregateRows_WhenTransactionOutsideColumns_IgnoresIt()
    {
        // Arrange
        var food = new Category { UserId = "u", Name = "Courses" };
        var transactions = new[]
        {
            Tx(food.Id, 10m, TransactionType.Debit, "MARCHE"),
            Tx(food.Id, 99m, TransactionType.Debit, "MARCHE", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc))
        };

        // Act
        var rows = AggregateCategories(transactions, [food]);

        // Assert
        rows.Should().ContainSingle().Subject.RowTotal.Should().Be(10m);
    }

    [Fact]
    public void AggregateRows_WhenMetricIsCountAndRowsAreSorted_ReturnsCountsOrderedByTotalDescending()
    {
        // Arrange
        var small = new Category { UserId = "u", Name = "A" };
        var big = new Category { UserId = "u", Name = "B" };
        var transactions = new[]
        {
            Tx(small.Id, 500m, TransactionType.Debit, "X"),
            Tx(big.Id, 1m, TransactionType.Debit, "Y"),
            Tx(big.Id, 1m, TransactionType.Debit, "Y")
        };

        // Act
        var rows = AggregateCategories(transactions, [small, big], PivotMetricType.Count);

        // Assert
        rows.Select(r => r.Label).Should().Equal("B", "A");
        rows[0].RowTotal.Should().Be(2m);
        rows[0].CellValues["2026-01"].Should().Be(2m);
        rows[0].MonthlyAverage.Should().Be(1m);
    }
}
