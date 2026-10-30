using Ledger.Features.Accounts.Entities;
using StatementForge.Analysis;

namespace Ledger.Features.Imports.Services;

/// <summary>
/// Repère, parmi des transactions déjà enregistrées, les groupes de doublons probables :
/// d'abord ceux du même jour, puis ceux à dates proches.
/// </summary>
internal static class ExistingDuplicateDetector
{
    // Tolérance minimale de la seconde passe, quel que soit le réglage de l'utilisateur.
    private const int MinimumClusterToleranceDays = 5;

    private delegate bool CandidateMatcher(Transaction anchor, HashSet<string> anchorTokens, Transaction candidate);

    public static List<ExistingDuplicateGroupDto> Detect(IReadOnlyList<Transaction> transactions, int configuredToleranceDays)
    {
        var groups = new List<ExistingDuplicateGroupDto>();
        var processed = new HashSet<Guid>();

        DetectSameDayDuplicates(transactions, processed, groups);
        DetectNearbyDuplicates(transactions, Math.Max(configuredToleranceDays, MinimumClusterToleranceDays), processed, groups);

        return groups.OrderByDescending(g => g.Date).ToList();
    }

    private static void DetectSameDayDuplicates(
        IReadOnlyList<Transaction> transactions,
        HashSet<Guid> processed,
        List<ExistingDuplicateGroupDto> groups)
    {
        foreach (var grp in transactions.GroupBy(t => (t.Date.Date, t.Amount, t.Type)))
        {
            if (grp.Count() <= 1)
            {
                continue;
            }

            var candidates = grp.Where(t => !processed.Contains(t.Id)).ToList();
            foreach (var cluster in BuildClusters(
                candidates,
                processed,
                (_, anchorTokens, candidate) => StatementLabelAnalyzer.IsTokenSetMatching(anchorTokens, TokensOf(candidate), sameDate: true)))
            {
                var key = $"{grp.Key.Date:yyyyMMdd}_{grp.Key.Amount}_{grp.Key.Type}_{cluster[0].Id}";
                groups.Add(new ExistingDuplicateGroupDto(key, grp.Key.Date, grp.Key.Amount, grp.Key.Type, cluster));
            }
        }
    }

    private static void DetectNearbyDuplicates(
        IReadOnlyList<Transaction> transactions,
        int toleranceDays,
        HashSet<Guid> processed,
        List<ExistingDuplicateGroupDto> groups)
    {
        var remaining = transactions
            .Where(t => !processed.Contains(t.Id))
            .GroupBy(t => (t.Amount, t.Type))
            .ToList();

        foreach (var grp in remaining)
        {
            if (grp.Count() <= 1)
            {
                continue;
            }

            var ordered = grp.OrderBy(t => t.Date).ToList();
            foreach (var cluster in BuildClusters(ordered, processed, (anchor, anchorTokens, candidate) =>
                         Math.Abs((candidate.Date.Date - anchor.Date.Date).TotalDays) <= toleranceDays
                         && StatementLabelAnalyzer.IsTokenSetMatching(anchorTokens, TokensOf(candidate), sameDate: false)))
            {
                var key = $"{cluster[0].Date:yyyyMMdd}_{grp.Key.Amount}_{grp.Key.Type}_{cluster[0].Id}";
                groups.Add(new ExistingDuplicateGroupDto(key, cluster[0].Date, grp.Key.Amount, grp.Key.Type, cluster));
            }
        }
    }

    // Chaque transaction non encore traitée sert d'ancre ; les suivantes qui lui correspondent rejoignent son groupe.
    // Seuls les groupes d'au moins deux transactions sont conservés, et leurs membres sont marqués traités.
    private static List<List<Transaction>> BuildClusters(
        List<Transaction> candidates,
        HashSet<Guid> processed,
        CandidateMatcher matches)
    {
        var clusters = new List<List<Transaction>>();

        for (int i = 0; i < candidates.Count; i++)
        {
            if (processed.Contains(candidates[i].Id))
            {
                continue;
            }

            var cluster = new List<Transaction> { candidates[i] };
            var anchorTokens = TokensOf(candidates[i]);

            for (int j = i + 1; j < candidates.Count; j++)
            {
                if (processed.Contains(candidates[j].Id) || !matches(candidates[i], anchorTokens, candidates[j]))
                {
                    continue;
                }

                cluster.Add(candidates[j]);
                processed.Add(candidates[j].Id);
            }

            if (cluster.Count > 1)
            {
                processed.Add(candidates[i].Id);
                clusters.Add(cluster);
            }
        }

        return clusters;
    }

    private static HashSet<string> TokensOf(Transaction transaction) =>
        StatementLabelAnalyzer.ExtractMeaningfulTokens(transaction.Label, transaction.RawLabel);
}
