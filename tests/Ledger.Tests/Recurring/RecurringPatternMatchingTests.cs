using FluentAssertions;
using Ledger.Features.Recurring.Services;
using Xunit;

namespace Ledger.Tests.Recurring;

public class RecurringPatternMatchingTests
{
    [Theory]
    [InlineData("PRLV SEPA NETFLIX", null, "NETFLIX", true)]
    [InlineData("PRLV SEPA SPOTIFY", null, "SPOTIFY", true)]
    [InlineData("PRLV EDF CLIENT 12345", null, "EDF", true)]
    [InlineData("PAIEMENT CB RESTAURANT LE BISTROT", null, "NETFLIX", false)]
    public void MatchesPattern_DirectSubstring_ShouldMatch(string label, string? rawLabel, string pattern, bool expected)
    {
        // Act
        var result = RecurringExpenseService.MatchesPattern(label, rawLabel, pattern);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("PRLV FREE TELECOM PARIS", null, "FREE*TELECOM", true)]
    [InlineData("PRLV FREE MOBILE 4G", null, "FREE*MOBILE", true)]
    [InlineData("PRLV FREEBOX MENSUEL", null, "FREE*MOBILE", false)]
    [InlineData("VIR SEPA DE ASSURANCE AXA FRANCE", null, "ASSURANCE%AXA", true)]
    public void MatchesPattern_Wildcards_ShouldMatch(string label, string? rawLabel, string pattern, bool expected)
    {
        // Act
        var result = RecurringExpenseService.MatchesPattern(label, rawLabel, pattern);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MatchesPattern_WhenInRawLabelOnly_ShouldMatch()
    {
        // Arrange
        var label = "PRLV SEPA 9876543";
        var rawLabel = "EMETTEUR AXA FRANCE CONTRAT AUTO REF 4567";
        var pattern = "AXA*AUTO";

        // Act
        var result = RecurringExpenseService.MatchesPattern(label, rawLabel, pattern);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("PRLV SEPA AXA ASSURANCES", "CONTRAT AUTO N123456", "AXA AUTO")]
    [InlineData("PRLV SEPA AXA IARD", "CONTRAT HABITATION 789", "AXA HABITATION")]
    [InlineData("PRLV FREE TELECOM", "FACTURE FIBRE INTERNET", "FREE FIBRE")]
    [InlineData("PRLV EDF CLIENT 54321", null, "EDF")]
    public void CleanTransactionLabel_WhenDescriptorPresent_PreservesContractDescriptor(string label, string? rawLabel, string expectedPrefix)
    {
        // Act
        var cleaned = RecurringExpenseService.CleanTransactionLabel(label, rawLabel);

        // Assert
        cleaned.Should().StartWith(expectedPrefix);
    }

    [Theory]
    [InlineData(25.00, 0.05, 20.00, false)] // 25€ ±5% (23.75 - 26.25) vs 20€ -> false
    [InlineData(5.99, 0.05, 20.00, false)]  // 5.99€ ±5% (5.69 - 6.29) vs 20€ -> false
    [InlineData(25.00, 0.05, 25.00, true)]  // 25€ vs 25€ -> true
    [InlineData(25.00, 0.05, 24.00, true)]  // 25€ vs 24€ -> true (within 5%)
    [InlineData(5.99, 0.05, 5.99, true)]    // 5.99€ vs 5.99€ -> true
    [InlineData(0.00, 0.05, 20.00, true)]   // 0€ expected (variable) -> true
    [InlineData(100.00, 0.20, 85.00, true)] // 100€ ±20% vs 85€ -> true
    [InlineData(100.00, 0.20, 75.00, false)]// 100€ ±20% vs 75€ -> false
    [InlineData(25.00, 0.00, 25.00, true)]  // Strict tolerance 0%
    [InlineData(25.00, 0.00, 24.50, false)] // Strict tolerance 0%
    public void MatchesAmount_WhenAmountWithinTolerance_ReturnsTrue(double expectedAmount, double tolerance, double actualAmount, bool expectedResult)
    {
        // Act
        var result = RecurringExpenseService.MatchesAmount((decimal)expectedAmount, (decimal)tolerance, (decimal)actualAmount);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void UserScenario_Pixpay20Euros_ShouldNotMatchRuleAt25EurosOrRuleAt599Euros()
    {
        // Arrange
        var txLabel = "Pixpay Paris Argent de poche - PAIEMENT PAR CARTE X0305 Pixpay Paris 08/10";
        var txAmount = 20.00m;

        var rule1 = new Ledger.Features.Recurring.Entities.RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Name = "Argent de poche",
            Pattern = "PIXPAY PARIS",
            ExpectedAmount = 25.00m,
            AmountTolerancePercentage = 0.05m
        };

        var rule2 = new Ledger.Features.Recurring.Entities.RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Name = "Abonnement Pixpay",
            Pattern = "X0305 PIXPAY",
            ExpectedAmount = 5.99m,
            AmountTolerancePercentage = 0.05m
        };

        // Act - pattern matches both
        var rule1PatternMatch = RecurringExpenseService.MatchesPattern(txLabel, null, rule1.Pattern);
        var rule2PatternMatch = RecurringExpenseService.MatchesPattern(txLabel, null, rule2.Pattern);

        // Act - amount matches neither
        var rule1AmountMatch = rule1.MatchesAmount(txAmount);
        var rule2AmountMatch = rule2.MatchesAmount(txAmount);

        // Assert
        rule1PatternMatch.Should().BeTrue();
        rule2PatternMatch.Should().BeTrue();

        rule1AmountMatch.Should().BeFalse();
        rule2AmountMatch.Should().BeFalse();
    }
}
