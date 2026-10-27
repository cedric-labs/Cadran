using AwesomeAssertions;
using Ledger.Domain.ValueObjects;
using Xunit;

namespace Ledger.Tests.Domain.ValueObjects;

public class IdentifiersTests
{
    [Fact]
    public void BankAccountId_WhenValidGuid_InstantiatesAndConvertsCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = new BankAccountId(guid);

        // Assert
        id.Value.Should().Be(guid);
        id.ToString().Should().Be(guid.ToString());
        ((Guid)id).Should().Be(guid);
    }

    [Fact]
    public void BankAccountId_WhenEmptyGuid_ThrowsArgumentException()
    {
        // Act
        var act = () => new BankAccountId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*L'identifiant de compte bancaire ne peut pas être un GUID vide*");
    }

    [Fact]
    public void BankAccountId_TryParse_WorksCorrectly()
    {
        // Act & Assert
        BankAccountId.TryParse(Guid.NewGuid().ToString(), out var validId).Should().BeTrue();
        validId.Should().NotBeNull();

        BankAccountId.TryParse(Guid.Empty.ToString(), out var emptyId).Should().BeFalse();
        emptyId.Should().BeNull();

        BankAccountId.TryParse("not-a-guid", out var invalidId).Should().BeFalse();
        invalidId.Should().BeNull();
    }

    [Fact]
    public void TransactionId_WhenValidGuid_InstantiatesAndConvertsCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = new TransactionId(guid);

        // Assert
        id.Value.Should().Be(guid);
        TransactionId.New().Value.Should().NotBeEmpty();
    }

    [Fact]
    public void TransactionId_WhenEmptyGuid_ThrowsArgumentException()
    {
        // Act
        var act = () => new TransactionId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CategoryId_WhenValidGuid_InstantiatesAndConvertsCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = new CategoryId(guid);

        // Assert
        id.Value.Should().Be(guid);
        CategoryId.New().Value.Should().NotBeEmpty();
    }

    [Fact]
    public void CategoryId_WhenEmptyGuid_ThrowsArgumentException()
    {
        // Act
        var act = () => new CategoryId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
