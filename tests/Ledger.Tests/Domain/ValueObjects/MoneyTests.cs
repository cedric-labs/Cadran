using FluentAssertions;
using Ledger.Domain.ValueObjects;
using Xunit;

namespace Ledger.Tests.Domain.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Addition_WhenSameCurrency_ReturnsSummedAmount()
    {
        // Arrange
        var m1 = new Money(50.25m, CurrencyCode.Eur);
        var m2 = new Money(20.75m, CurrencyCode.Eur);

        // Act
        var result = m1 + m2;

        // Assert
        result.Amount.Should().Be(71.00m);
        result.Currency.Should().Be(CurrencyCode.Eur);
    }

    [Fact]
    public void Addition_WhenDifferentCurrencies_ThrowsInvalidOperationException()
    {
        // Arrange
        var eur = new Money(50m, CurrencyCode.Eur);
        var usd = new Money(50m, CurrencyCode.Usd);

        // Act
        var act = () => eur + usd;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Opération arithmétique impossible entre devises distinctes*");
    }

    [Fact]
    public void Subtraction_WhenSameCurrency_ReturnsDifference()
    {
        // Arrange
        var m1 = new Money(100m, CurrencyCode.Eur);
        var m2 = new Money(40m, CurrencyCode.Eur);

        // Act
        var result = m1 - m2;

        // Assert
        result.Amount.Should().Be(60m);
    }

    [Fact]
    public void MultiplicationAndDivision_WithScalar_ComputesCorrectly()
    {
        // Arrange
        var m = new Money(100m, CurrencyCode.Eur);

        // Act & Assert
        (m * 2.5m).Amount.Should().Be(250m);
        (3m * m).Amount.Should().Be(300m);
        (m / 4m).Amount.Should().Be(25m);
    }

    [Fact]
    public void Division_ByZero_ThrowsDivideByZeroException()
    {
        // Arrange
        var m = new Money(100m, CurrencyCode.Eur);

        // Act
        var act = () => m / 0m;

        // Assert
        act.Should().Throw<DivideByZeroException>();
    }

    [Fact]
    public void NegateAndAbs_WhenCalledOnPositiveAmount_ReturnsExpectedNegatedAndAbsoluteValues()
    {
        // Arrange
        var m = new Money(150m, CurrencyCode.Eur);

        // Act & Assert
        (-m).Amount.Should().Be(-150m);
        (-m).IsNegative.Should().BeTrue();
        (-m).Abs().Amount.Should().Be(150m);
        m.IsPositive.Should().BeTrue();
        Money.Zero(CurrencyCode.Eur).IsZero.Should().BeTrue();
    }

    [Fact]
    public void Comparisons_WhenSameCurrency_CompareAmounts()
    {
        // Arrange
        var smaller = new Money(10m, CurrencyCode.Eur);
        var larger = new Money(20m, CurrencyCode.Eur);

        // Act & Assert
        (smaller < larger).Should().BeTrue();
        (larger > smaller).Should().BeTrue();
        (smaller <= larger).Should().BeTrue();
        (larger >= smaller).Should().BeTrue();
        smaller.CompareTo(larger).Should().BeNegative();
    }
}
