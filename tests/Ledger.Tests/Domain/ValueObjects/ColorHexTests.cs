using AwesomeAssertions;
using Ledger.Domain.ValueObjects;
using Xunit;

namespace Ledger.Tests.Domain.ValueObjects;

public class ColorHexTests
{
    [Theory]
    [InlineData("#3b82f6", "#3b82f6")]
    [InlineData("#FFF", "#fff")]
    [InlineData(" #10B981 ", "#10b981")]
    [InlineData("#12345678", "#12345678")]
    public void Constructeur_WhenValidHexCode_NormalizesToLowercase(string input, string expected)
    {
        // Act
        var color = new ColorHex(input);

        // Assert
        color.Value.Should().Be(expected);
        color.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("3b82f6")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#gggggg")]
    [InlineData("#123456789")]
    public void Constructeur_WhenInvalidHex_ThrowsArgumentException(string input)
    {
        // Act
        var act = () => new ColorHex(input);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryParse_WhenValidHex_ReturnsTrueAndOutputsColorHex()
    {
        // Act
        var success = ColorHex.TryParse("#ef4444", out var color);

        // Assert
        success.Should().BeTrue();
        color.Should().NotBeNull();
        color.Value.Value.Should().Be("#ef4444");
    }

    [Fact]
    public void TryParse_WhenInvalidHex_ReturnsFalseAndOutputsNull()
    {
        // Act
        var success = ColorHex.TryParse("not-a-color", out var color);

        // Assert
        success.Should().BeFalse();
        color.Should().BeNull();
    }

    [Fact]
    public void FromOrDefault_WhenNullOrInvalid_ReturnsDefaultBlue()
    {
        // Act & Assert
        ColorHex.FromOrDefault(null).Should().Be(ColorHex.Default);
        ColorHex.FromOrDefault("invalid").Should().Be(ColorHex.Default);
        ColorHex.FromOrDefault("#64748b").Should().Be(ColorHex.Slate);
    }
}
