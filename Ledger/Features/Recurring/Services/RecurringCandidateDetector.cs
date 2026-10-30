using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Models;

namespace Ledger.Features.Recurring.Services;

/// <summary>
/// Règle récurrente existante, nom et motif en minuscules, utilisée pour repérer les charges déjà suivies.
/// </summary>
internal sealed record ExistingRecurringRule(Guid Id, string Name, string Pattern);

/// <summary>
/// Détecte les charges récurrentes probables dans un historique de débits.
/// </summary>
internal sealed class RecurringCandidateDetector(
    IReadOnlyList<ExistingRecurringRule> existingRules,
    IReadOnlyList<string> ignoredPatterns)
{
    // Au-delà de ce rapport max/min, le montant varie trop pour un abonnement.
    private const decimal MaximumAmountSpreadRatio = 2.5m;

    public List<RecurringCandidateDto> Detect(IEnumerable<Transaction> debits)
    {
        var candidates = new List<RecurringCandidateDto>();

        foreach (var (groupKey, group) in IssuerTransactionGrouper.Group(debits))
        {
            var ordered = group.OrderBy(t => t.Date).ToList();
            if (ordered.Count < 2)
            {
                continue;
            }

            var pattern = ExtractPattern(groupKey);
            if (IsIgnored(pattern, groupKey)
                || !RecurrenceCadenceClassifier.TryClassify(ordered, out var frequency)
                || HasUnstableAmounts(ordered))
            {
                continue;
            }

            candidates.Add(BuildCandidate(groupKey, pattern, frequency, ordered));
        }

        return candidates
            .OrderByDescending(c => c.ConfidenceScore)
            .ThenByDescending(c => c.OccurrencesCount)
            .ToList();
    }

    // Une clé de sous-groupe porte un suffixe de montant (« AXA (45.00 €) ») qui ne fait pas partie du motif de recherche.
    private static string ExtractPattern(string groupKey)
    {
        var parenthesis = groupKey.IndexOf('(', StringComparison.Ordinal);
        return parenthesis >= 0 ? groupKey[..parenthesis].Trim() : groupKey.Trim();
    }

    private bool IsIgnored(string pattern, string groupKey) =>
        ignoredPatterns.Any(p =>
            string.Equals(p, pattern, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p, groupKey, StringComparison.OrdinalIgnoreCase) ||
            pattern.Contains(p, StringComparison.OrdinalIgnoreCase) ||
            p.Contains(pattern, StringComparison.OrdinalIgnoreCase));

    private static bool HasUnstableAmounts(List<Transaction> ordered)
    {
        var min = ordered.Min(t => t.Amount);
        var max = ordered.Max(t => t.Amount);
        return min > 0 && max / min > MaximumAmountSpreadRatio;
    }

    private ExistingRecurringRule? FindExistingRule(string pattern, string suggestedName)
    {
        var patternLower = pattern.ToLowerInvariant();
        var nameLower = suggestedName.Trim().ToLowerInvariant();

        return existingRules.FirstOrDefault(e =>
            string.Equals(e.Pattern, patternLower, StringComparison.Ordinal) ||
            patternLower.Contains(e.Pattern, StringComparison.Ordinal) ||
            e.Pattern.Contains(patternLower, StringComparison.Ordinal) ||
            string.Equals(e.Name, nameLower, StringComparison.Ordinal) ||
            nameLower.Contains(e.Name, StringComparison.Ordinal) ||
            e.Name.Contains(nameLower, StringComparison.Ordinal));
    }

    private RecurringCandidateDto BuildCandidate(
        string groupKey,
        string pattern,
        RecurringFrequency frequency,
        List<Transaction> ordered)
    {
        var suggestedName = RecurringExpenseService.FormatSuggestedName(groupKey);
        var existing = FindExistingRule(pattern, suggestedName);
        var amounts = ordered.Select(t => t.Amount).ToList();
        var days = ordered.Select(t => t.Date.Day).Order().ToList();
        var category = MostFrequent(ordered.Select(t => t.Category).OfType<Category>());
        var account = MostFrequent(ordered.Select(t => t.BankAccount).OfType<BankAccount>());

        return new RecurringCandidateDto
        {
            Pattern = pattern,
            SuggestedName = suggestedName,
            AverageAmount = Math.Round(amounts.Average(), 2),
            LastAmount = amounts[^1],
            ExpectedDayOfMonth = days[days.Count / 2],
            Frequency = frequency,
            SuggestedCategoryId = category?.Id,
            SuggestedCategoryName = category?.Name,
            SuggestedCategoryIcon = category?.IconName,
            SuggestedCategoryColor = category?.ColorHex,
            BankAccountId = account?.Id,
            BankAccountName = account?.Name,
            OccurrencesCount = ordered.Count,
            FirstDate = ordered[0].Date,
            LastDate = ordered[^1].Date,
            ConfidenceScore = ComputeConfidenceScore(ordered.Count, amounts.Max() - amounts.Min()),
            IsAlreadyConfigured = existing is not null,
            ConfiguredExpenseName = existing?.Name,
            SampleTransactions = ordered.TakeLast(3).Select(t => new TransactionSampleDto
            {
                Id = t.Id,
                Date = t.Date,
                Label = t.Label,
                Amount = t.Amount
            }).ToList()
        };
    }

    private static int ComputeConfidenceScore(int occurrences, decimal amountSpread)
    {
        int score = 50;
        if (occurrences >= 3) score += 20;
        if (occurrences >= 5) score += 15;
        if (Math.Abs(amountSpread) < 0.10m) score += 15;
        return Math.Min(98, score);
    }

    private static T? MostFrequent<T>(IEnumerable<T> items) where T : class =>
        items.GroupBy(i => i).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
}
