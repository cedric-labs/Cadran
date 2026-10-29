using AwesomeAssertions;
using Ledger.Domain.ValueObjects;
using Xunit;

namespace Ledger.Tests.Domain.ValueObjects;

public class CurrencyCodeTests
{
    [Theory]
    [InlineData("EUR", "EUR")]
    [InlineData("usd", "USD")]
    [InlineData(" gbp ", "GBP")]
    [InlineData("chf", "CHF")]
    public void Constructeur_WhenValid3LetterCode_NormalizesToUppercase(string input, string expected)
    {
        // Act
        var code = new CurrencyCode(input);

        // Assert
        code.Value.Should().Be(expected);
        code.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("123")]
    [InlineData("E1R")]
    [InlineData("€")]
    public void Constructeur_WhenInvalidCode_ThrowsArgumentException(string input)
    {
        // Act
        var act = () => new CurrencyCode(input);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryParse_WhenValidCode_ReturnsTrueAndOutputsCurrencyCode()
    {
        // Act
        var success = CurrencyCode.TryParse("jpy", out var code);

        // Assert
        success.Should().BeTrue();
        code.Should().NotBeNull();
        code.Value.Value.Should().Be("JPY");
    }

    [Fact]
    public void TryParse_WhenInvalidCode_ReturnsFalseAndOutputsNull()
    {
        // Act
        var success = CurrencyCode.TryParse("invalid", out var code);

        // Assert
        success.Should().BeFalse();
        code.Should().BeNull();
    }

    [Fact]
    public void FromOrDefault_WhenNullOrInvalid_ReturnsEur()
    {
        // Act & Assert
        CurrencyCode.FromOrDefault(null).Should().Be(CurrencyCode.Eur);
        CurrencyCode.FromOrDefault("invalid").Should().Be(CurrencyCode.Eur);
        CurrencyCode.FromOrDefault("usd").Should().Be(CurrencyCode.Usd);
    }
}
