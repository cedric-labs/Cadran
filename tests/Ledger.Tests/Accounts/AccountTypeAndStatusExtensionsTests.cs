using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Xunit;

namespace Ledger.Tests.Accounts;

public class AccountTypeAndStatusExtensionsTests
{
    [Fact]
    public void ToDisplayString_WhenInvoked_CoversAllEnumsAndBranches()
    {
        var allTypes = Enum.GetValues<AccountType>();

        foreach (var type in allTypes)
        {
            var displayName = type.GetDisplayName();
            displayName.Should().NotBeNullOrWhiteSpace();

            var badgeClass = type.GetBadgeClass();
            badgeClass.Should().NotBeNullOrWhiteSpace();

            var isLiquid = type.IsLiquid();
            if (type is AccountType.Checking or AccountType.Cash)
            {
                isLiquid.Should().BeTrue();
            }
            else
            {
                isLiquid.Should().BeFalse();
            }

            var isSavings = type.IsSavingsOrInvestment();
            if (type is AccountType.Checking or AccountType.CreditCard or AccountType.Cash)
            {
                isSavings.Should().BeFalse();
            }
            else
            {
                isSavings.Should().BeTrue();
            }
        }

        // Test with invalid enum values to cover fallback branches
        var undefinedType = (AccountType)9999;
        undefinedType.GetDisplayName().Should().Be("9999");
        undefinedType.GetBadgeClass().Should().Be("bg-secondary-subtle text-secondary");
        undefinedType.IsLiquid().Should().BeFalse();
        undefinedType.IsSavingsOrInvestment().Should().BeFalse();
    }

    [Fact]
    public void ToDisplayBadge_WhenInvoked_CoversAllEnumsAndBranches()
    {
        var allStatuses = Enum.GetValues<TransactionStatus>();

        foreach (var status in allStatuses)
        {
            status.ToDisplayName().Should().NotBeNullOrWhiteSpace();
            status.ToBadgeClass().Should().NotBeNullOrWhiteSpace();
            status.ToIconClass().Should().NotBeNullOrWhiteSpace();
        }

        var undefinedStatus = (TransactionStatus)9999;
        undefinedStatus.ToDisplayName().Should().Be("9999");
        undefinedStatus.ToBadgeClass().Should().Be("bg-secondary-subtle text-secondary");
        undefinedStatus.ToIconClass().Should().Be("bi-circle");
    }

    [Fact]
    public void Transaction_GetStatusMethods_CoverAllBranches()
    {
        // 1. Transaction avec statut standard (Reconciled, Cleared, Pending, Autre)
        var txReconciled = new Transaction { UserId = "user", Label = "Test", Status = TransactionStatus.Reconciled };
        txReconciled.GetStatusDisplayName().Should().Be("Pointée");
        txReconciled.GetStatusColorHex().Should().Be("#198754");
        txReconciled.GetStatusIconClass().Should().Be("bi-check2-all");

        var txCleared = new Transaction { UserId = "user", Label = "Test", Status = TransactionStatus.Cleared };
        txCleared.GetStatusDisplayName().Should().Be("Comptabilisée");
        txCleared.GetStatusColorHex().Should().Be("#0dcaf0");
        txCleared.GetStatusIconClass().Should().Be("bi-check-lg");

        var txPending = new Transaction { UserId = "user", Label = "Test", Status = TransactionStatus.Pending };
        txPending.GetStatusDisplayName().Should().Be("En attente");
        txPending.GetStatusColorHex().Should().Be("#ffc107");
        txPending.GetStatusIconClass().Should().Be("bi-hourglass-split");

        var txUnknown = new Transaction { UserId = "user", Label = "Test", Status = (TransactionStatus)9999 };
        txUnknown.GetStatusColorHex().Should().Be("#6c757d");

        // 2. Transaction avec CustomStatus
        var customStatus = new TransactionCustomStatus
        {
            Id = Guid.NewGuid(),
            UserId = "user",
            Name = "Vérifiée manuellement",
            ColorHex = "#8b5cf6",
            IconName = "bi-patch-check"
        };
        var txCustom = new Transaction
        {
            UserId = "user",
            Label = "Test Custom",
            CustomStatus = customStatus
        };
        txCustom.GetStatusDisplayName().Should().Be("Vérifiée manuellement");
        txCustom.GetStatusColorHex().Should().Be("#8b5cf6");
        txCustom.GetStatusIconClass().Should().Be("bi-patch-check");
    }
}
