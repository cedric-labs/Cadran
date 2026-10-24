using FluentAssertions;
using Ledger.Features.Accounts.Services;
using Xunit;

namespace Ledger.Tests.Accounts;

public class BudgetPeriodCalculatorTests
{
    [Fact]
    public void GetCurrentMonthRange_DefaultSalaryStartDay1_ReturnsCalendarMonth()
    {
        // Arrange
        var today = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var (start, end, period) = BudgetPeriodCalculator.GetCurrentMonthRange(today, salaryMonthStartDay: 1);

        // Assert
        start.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc));
        period.Should().Be(BudgetPeriodCalculator.CurrentMonth);
    }

    [Fact]
    public void GetCurrentMonthRange_WhenBeforePaydayWithSalaryStartDay25_ReturnsPreviousMonth25ToCurrent24()
    {
        // Arrange : aujourd'hui le 14 octobre, avant le 25
        var today = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var (start, end, period) = BudgetPeriodCalculator.GetCurrentMonthRange(today, salaryMonthStartDay: 25);

        // Assert : période du 25/09/2026 au 24/10/2026
        start.Should().Be(new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc));
        period.Should().Be(BudgetPeriodCalculator.CurrentMonth);
    }

    [Fact]
    public void GetCurrentMonthRange_WhenOnOrAfterPaydayWithSalaryStartDay25_ReturnsCurrent25ToNext24()
    {
        // Arrange : aujourd'hui le 26 octobre, après le 25
        var today = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var (start, end, period) = BudgetPeriodCalculator.GetCurrentMonthRange(today, salaryMonthStartDay: 25);

        // Assert : période du 25/10/2026 au 24/11/2026
        start.Should().Be(new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc));
        period.Should().Be(BudgetPeriodCalculator.CurrentMonth);
    }

    [Fact]
    public void GetCurrentMonthRange_WhenYearBoundaryWithSalaryStartDay25_ReturnsCorrectDates()
    {
        // Arrange : aujourd'hui le 10 janvier 2026 (avant le 25)
        var today = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var (start, end, period) = BudgetPeriodCalculator.GetCurrentMonthRange(today, salaryMonthStartDay: 25);

        // Assert : période du 25/12/2025 au 24/01/2026
        start.Should().Be(new DateTime(2025, 12, 25, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 1, 24, 0, 0, 0, DateTimeKind.Utc));
        period.Should().Be(BudgetPeriodCalculator.CurrentMonth);
    }

    [Fact]
    public void GetLastMonthRange_SalaryStartDay25_ReturnsPreviousPeriod()
    {
        // Arrange : aujourd'hui le 14 octobre 2026 (période courante 25/09 -> 24/10)
        var today = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var (start, end, period) = BudgetPeriodCalculator.GetLastMonthRange(today, salaryMonthStartDay: 25);

        // Assert : période précédente du 25/08/2026 au 24/09/2026
        start.Should().Be(new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc));
        period.Should().Be(BudgetPeriodCalculator.LastMonth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(29)]
    [InlineData(31)]
    public void GetCurrentMonthRange_InvalidSalaryStartDay_FallsBackToStandardMonth(int invalidDay)
    {
        // Arrange
        var today = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var (start, end, _) = BudgetPeriodCalculator.GetCurrentMonthRange(today, salaryMonthStartDay: invalidDay);

        // Assert : retombe sur le 1er au 31 mai
        start.Should().Be(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ResolvePeriod_CurrentMonthWithSalaryStartDay_ResolvesCorrectBounds()
    {
        // Arrange
        var today = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var (start, end, period) = BudgetPeriodCalculator.ResolvePeriod(
            requestedPeriod: BudgetPeriodCalculator.CurrentMonth,
            requestedStartDate: null,
            requestedEndDate: null,
            salaryMonthStartDay: 25,
            today: today);

        // Assert
        start.Should().Be(new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc));
        period.Should().Be(BudgetPeriodCalculator.CurrentMonth);
    }

    [Fact]
    public void ResolvePeriod_All_ReturnsNullBounds()
    {
        var (start, end, period) = BudgetPeriodCalculator.ResolvePeriod(
            requestedPeriod: BudgetPeriodCalculator.All,
            requestedStartDate: null,
            requestedEndDate: null);

        start.Should().BeNull();
        end.Should().BeNull();
        period.Should().Be(BudgetPeriodCalculator.All);
    }

    [Theory]
    [InlineData(BudgetPeriodCalculator.TwoWeeks, -14)]
    public void ResolvePeriod_RelativeDays_ReturnsAccurateBounds(string preset, int offsetDays)
    {
        var today = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var (start, end, period) = BudgetPeriodCalculator.ResolvePeriod(
            requestedPeriod: preset,
            requestedStartDate: null,
            requestedEndDate: null,
            today: today);

        start.Should().Be(today.AddDays(offsetDays));
        end.Should().Be(today);
        period.Should().Be(preset);
    }

    [Theory]
    [InlineData(BudgetPeriodCalculator.ThreeMonths, -3)]
    [InlineData(BudgetPeriodCalculator.SixMonths, -6)]
    public void ResolvePeriod_RelativeMonths_ReturnsAccurateBounds(string preset, int offsetMonths)
    {
        var today = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var (start, end, period) = BudgetPeriodCalculator.ResolvePeriod(
            requestedPeriod: preset,
            requestedStartDate: null,
            requestedEndDate: null,
            today: today);

        start.Should().Be(today.AddMonths(offsetMonths));
        end.Should().Be(today);
        period.Should().Be(preset);
    }

    [Fact]
    public void ResolvePeriod_WithManualDates_DetectsCustomPreset()
    {
        var today = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var customStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var customEnd = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);

        var (start, end, period) = BudgetPeriodCalculator.ResolvePeriod(
            requestedPeriod: null,
            requestedStartDate: customStart,
            requestedEndDate: customEnd,
            today: today);

        start.Should().Be(customStart);
        end.Should().Be(customEnd);
        period.Should().Be(BudgetPeriodCalculator.Custom);
    }

    [Fact]
    public void ResolvePeriod_WithManualDatesMatchingPresets_DetectsExactPreset()
    {
        var today = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        // 1. Matches CurrentMonth (standard 1st of month)
        var (curStart, curEnd, _) = BudgetPeriodCalculator.GetCurrentMonthRange(today, 1);
        var (_, _, pCur) = BudgetPeriodCalculator.ResolvePeriod(null, curStart, curEnd, today: today);
        pCur.Should().Be(BudgetPeriodCalculator.CurrentMonth);

        // 2. Matches LastMonth
        var (lastStart, lastEnd, _) = BudgetPeriodCalculator.GetLastMonthRange(today, 1);
        var (_, _, pLast) = BudgetPeriodCalculator.ResolvePeriod(null, lastStart, lastEnd, today: today);
        pLast.Should().Be(BudgetPeriodCalculator.LastMonth);

        // 3. Matches TwoWeeks
        var (_, _, pTwoWeeks) = BudgetPeriodCalculator.ResolvePeriod(null, today.AddDays(-14), today, today: today);
        pTwoWeeks.Should().Be(BudgetPeriodCalculator.TwoWeeks);

        // 4. Matches ThreeMonths
        var (_, _, pThreeMonths) = BudgetPeriodCalculator.ResolvePeriod(null, today.AddMonths(-3), today, today: today);
        pThreeMonths.Should().Be(BudgetPeriodCalculator.ThreeMonths);

        // 5. Matches SixMonths
        var (_, _, pSixMonths) = BudgetPeriodCalculator.ResolvePeriod(null, today.AddMonths(-6), today, today: today);
        pSixMonths.Should().Be(BudgetPeriodCalculator.SixMonths);
    }

    [Fact]
    public void ResolvePeriod_WithOnlyOneManualDate_FallsBackToRequestedOrCustom()
    {
        var today = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var singleDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Only start date with requestedPeriod
        var (s1, e1, p1) = BudgetPeriodCalculator.ResolvePeriod("my-requested", singleDate, null, today: today);
        s1.Should().Be(singleDate);
        e1.Should().BeNull();
        p1.Should().Be("my-requested");

        // Only end date without requestedPeriod
        var (s2, e2, p2) = BudgetPeriodCalculator.ResolvePeriod(null, null, singleDate, today: today);
        s2.Should().BeNull();
        e2.Should().Be(singleDate);
        p2.Should().Be(BudgetPeriodCalculator.Custom);
    }

    [Fact]
    public void ResolvePeriod_WithFallbackDefaultsAndUnknownPresets_ResolvesCorrectly()
    {
        var today = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        // requestedPeriod null, uses defaultPeriodFallback
        var (_, _, pFallback) = BudgetPeriodCalculator.ResolvePeriod(null, null, null, defaultPeriodFallback: BudgetPeriodCalculator.TwoWeeks, today: today);
        pFallback.Should().Be(BudgetPeriodCalculator.TwoWeeks);

        // requestedPeriod null, defaultPeriodFallback null, defaults to CurrentMonth
        var (_, _, pDefault) = BudgetPeriodCalculator.ResolvePeriod(null, null, null, defaultPeriodFallback: null, today: today);
        pDefault.Should().Be(BudgetPeriodCalculator.CurrentMonth);

        // Unrecognized preset string, falls back to CurrentMonth range
        var (sUnk, eUnk, pUnk) = BudgetPeriodCalculator.ResolvePeriod("unknown-preset", null, null, today: today);
        pUnk.Should().Be(BudgetPeriodCalculator.CurrentMonth);
        sUnk.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        eUnk.Should().Be(new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc));
    }
}
