using AwesomeAssertions;
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
}
