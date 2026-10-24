using FluentAssertions;
using Ledger.Features.Accounts.Entities;
using Xunit;

namespace Ledger.Tests.Accounts;

public class AccountAndTransactionExtensionsTests
{
    [Theory]
    [InlineData(AccountType.Checking, "Compte de Dépôt")]
    [InlineData(AccountType.LivretA, "Livret A")]
    [InlineData(AccountType.CreditCard, "Carte à débit différé / Crédit")]
    [InlineData(AccountType.Cash, "Portefeuille / Espèces")]
    [InlineData(AccountType.Pea, "Plan d'Épargne en Actions (PEA)")]
    public void AccountType_GetDisplayName_ShouldReturnExpectedDisplayName(AccountType type, string expectedName)
    {
        type.GetDisplayName().Should().Be(expectedName);
    }

    [Theory]
    [InlineData(AccountType.Checking, true)]
    [InlineData(AccountType.Cash, true)]
    [InlineData(AccountType.LivretA, false)]
    [InlineData(AccountType.Pea, false)]
    public void AccountType_IsLiquid_ShouldClassifyLiquidAccountsAccurately(AccountType type, bool expectedLiquid)
    {
        type.IsLiquid().Should().Be(expectedLiquid);
    }

    [Theory]
    [InlineData(AccountType.Checking, false)]
    [InlineData(AccountType.Cash, false)]
    [InlineData(AccountType.LivretA, true)]
    [InlineData(AccountType.Ldds, true)]
    [InlineData(AccountType.Lep, true)]
    [InlineData(AccountType.Savings, true)]
    [InlineData(AccountType.Pea, true)]
    [InlineData(AccountType.Investment, true)]
    [InlineData(AccountType.Crypto, true)]
    public void AccountType_IsSavingsOrInvestment_ShouldClassifyCorrectly(AccountType type, bool expectedSavings)
    {
        type.IsSavingsOrInvestment().Should().Be(expectedSavings);
    }

    [Theory]
    [InlineData(AccountType.Checking, "bg-primary-subtle text-primary")]
    [InlineData(AccountType.CreditCard, "bg-warning-subtle text-warning")]
    [InlineData(AccountType.Cash, "bg-secondary-subtle text-secondary")]
    [InlineData(AccountType.LivretA, "bg-info-subtle text-info")]
    [InlineData(AccountType.Pea, "bg-success-subtle text-success")]
    public void AccountType_GetBadgeClass_ShouldReturnCorrectCssClass(AccountType type, string expectedClass)
    {
        type.GetBadgeClass().Should().Be(expectedClass);
    }

    [Theory]
    [InlineData(TransactionStatus.Pending, "En attente")]
    [InlineData(TransactionStatus.Cleared, "Comptabilisée")]
    [InlineData(TransactionStatus.Reconciled, "Pointée")]
    public void TransactionStatus_ToDisplayName_ShouldReturnExpectedFrenchName(TransactionStatus status, string expectedName)
    {
        status.ToDisplayName().Should().Be(expectedName);
    }

    [Theory]
    [InlineData(TransactionStatus.Pending, "bg-warning-subtle text-warning-emphasis border border-warning-subtle")]
    [InlineData(TransactionStatus.Cleared, "bg-info-subtle text-info-emphasis border border-info-subtle")]
    [InlineData(TransactionStatus.Reconciled, "bg-success-subtle text-success-emphasis border border-success-subtle")]
    public void TransactionStatus_ToBadgeClass_ShouldReturnBadgeClass(TransactionStatus status, string expectedClass)
    {
        status.ToBadgeClass().Should().Be(expectedClass);
    }

    [Theory]
    [InlineData(TransactionStatus.Pending, "bi-hourglass-split")]
    [InlineData(TransactionStatus.Cleared, "bi-check-lg")]
    [InlineData(TransactionStatus.Reconciled, "bi-check2-all")]
    public void TransactionStatus_ToIconClass_ShouldReturnIconClass(TransactionStatus status, string expectedIcon)
    {
        status.ToIconClass().Should().Be(expectedIcon);
    }
}
