using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;

namespace Ledger.Tests.Accounts;

public sealed class ManualEntryMatchFinderTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly DateTime Day = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    private static Transaction Tx(
        string label,
        decimal amount = 25m,
        int dayOffset = 0,
        TransactionType type = TransactionType.Debit,
        Guid? accountId = null,
        TransactionOrigin origin = TransactionOrigin.Import) => new()
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = accountId ?? AccountId,
            Label = label,
            Amount = amount,
            Type = type,
            Date = Day.AddDays(dayOffset),
            Origin = origin,
        };

    private static Transaction Manual(string label, decimal amount = 25m, int dayOffset = 0, TransactionType type = TransactionType.Debit, Guid? accountId = null) =>
        Tx(label, amount, dayOffset, type, accountId, TransactionOrigin.Manual);

    [Fact]
    public void Find_WhenAmountTypeAccountAndDateAgree_PairsManualWithImported()
    {
        var manual = Manual("Courses");
        var imported = Tx("CB CARREFOUR 20/10", dayOffset: 1);

        var matches = ManualEntryMatchFinder.Find([manual], [imported], toleranceDays: 5);

        matches.Should().ContainSingle();
        matches[0].Manual.Should().BeSameAs(manual);
        matches[0].Imported.Should().BeSameAs(imported);
        matches[0].DayGap.Should().Be(1);
    }

    [Fact]
    public void Find_WhenAmountDiffers_ReturnsNoPair()
    {
        var matches = ManualEntryMatchFinder.Find([Manual("Courses")], [Tx("CARREFOUR", amount: 25.01m)], 5);

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Find_WhenTypeDiffers_ReturnsNoPair()
    {
        var matches = ManualEntryMatchFinder.Find([Manual("Courses")], [Tx("CARREFOUR", type: TransactionType.Credit)], 5);

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Find_WhenAccountDiffers_ReturnsNoPair()
    {
        var matches = ManualEntryMatchFinder.Find([Manual("Courses")], [Tx("CARREFOUR", accountId: Guid.NewGuid())], 5);

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Find_WhenGapEqualsTolerance_PairsButBeyondToleranceDoesNot()
    {
        var manual = Manual("Courses");

        ManualEntryMatchFinder.Find([manual], [Tx("A", dayOffset: 5)], 5).Should().ContainSingle();
        ManualEntryMatchFinder.Find([manual], [Tx("A", dayOffset: -6)], 5).Should().BeEmpty();
    }

    [Fact]
    public void Find_WhenLabelsShareNoWord_StillPairsOnAmountAndDate()
    {
        var matches = ManualEntryMatchFinder.Find([Manual("Courses")], [Tx("PRLV SEPA 8841")], 5);

        matches.Should().ContainSingle();
    }

    [Fact]
    public void Find_WhenSeveralImportedCandidates_PicksTheClosestDate()
    {
        var far = Tx("FAR", dayOffset: 4);
        var near = Tx("NEAR", dayOffset: -1);

        var matches = ManualEntryMatchFinder.Find([Manual("Courses")], [far, near], 5);

        matches.Should().ContainSingle().Which.Imported.Should().BeSameAs(near);
    }

    [Fact]
    public void Find_WhenSameGap_PrefersTheImportedSharingAWordWithTheManualLabel()
    {
        var other = Tx("PRLV ORANGE", dayOffset: 1);
        var sharing = Tx("CB CARREFOUR MARKET", dayOffset: 1);

        var matches = ManualEntryMatchFinder.Find([Manual("Carrefour")], [other, sharing], 5);

        matches.Should().ContainSingle().Which.Imported.Should().BeSameAs(sharing);
    }

    [Fact]
    public void Find_WhenTwoManualsCompeteForOneImported_PairsItOnlyOnce()
    {
        var closer = Manual("Courses A", dayOffset: 0);
        var farther = Manual("Courses B", dayOffset: 3);
        var imported = Tx("CARREFOUR", dayOffset: 1);

        var matches = ManualEntryMatchFinder.Find([farther, closer], [imported], 5);

        matches.Should().ContainSingle().Which.Manual.Should().BeSameAs(closer);
    }

    [Fact]
    public void Find_WhenTwoIdenticalManualsAndTwoImported_PairsEachOnce()
    {
        var m1 = Manual("Café", dayOffset: 0);
        var m2 = Manual("Café", dayOffset: 0);
        var i1 = Tx("CAFE 1", dayOffset: 0);
        var i2 = Tx("CAFE 2", dayOffset: 0);

        var matches = ManualEntryMatchFinder.Find([m1, m2], [i1, i2], 5);

        matches.Should().HaveCount(2);
        matches.Select(m => m.Manual).Should().OnlyHaveUniqueItems();
        matches.Select(m => m.Imported).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Find_WhenNoManualOrNoImported_ReturnsEmpty()
    {
        ManualEntryMatchFinder.Find([], [Tx("A")], 5).Should().BeEmpty();
        ManualEntryMatchFinder.Find([Manual("A")], [], 5).Should().BeEmpty();
    }

    [Fact]
    public void Find_WhenToleranceIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var act = () => ManualEntryMatchFinder.Find([Manual("A")], [Tx("A")], -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
