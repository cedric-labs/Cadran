using Ledger.Features.Accounts.Entities;
using Ledger.Features.Recurring.Entities;

namespace Ledger.Features.Recurring.Services;

/// <summary>
/// Déduit la fréquence d'une série de débits à partir de l'écart moyen entre occurrences consécutives.
/// </summary>
internal static class RecurrenceCadenceClassifier
{
    private const int MinimumOccurrencesForIrregularCadence = 3;

    public static bool TryClassify(IReadOnlyList<Transaction> ordered, out RecurringFrequency frequency)
    {
        frequency = RecurringFrequency.Monthly;

        var intervals = new List<int>();
        for (int i = 1; i < ordered.Count; i++)
        {
            var days = (ordered[i].Date - ordered[i - 1].Date).Days;
            if (days > 0)
            {
                intervals.Add(days);
            }
        }

        if (intervals.Count == 0)
        {
            return false;
        }

        var average = intervals.Average();
        bool isRegular;
        if (average is >= 25 and <= 36)
        {
            frequency = RecurringFrequency.Monthly;
            isRegular = intervals.All(d => d is >= 20 and <= 45);
        }
        else if (average is >= 80 and <= 100)
        {
            frequency = RecurringFrequency.Quarterly;
            isRegular = intervals.All(d => d is >= 75 and <= 110);
        }
        else if (average is >= 340 and <= 390)
        {
            frequency = RecurringFrequency.Annual;
            isRegular = true;
        }
        else
        {
            return false;
        }

        return isRegular || ordered.Count >= MinimumOccurrencesForIrregularCadence;
    }
}
