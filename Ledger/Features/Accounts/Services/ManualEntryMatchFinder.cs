using Ledger.Features.Accounts.Entities;
using StatementForge.Analysis;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Paire candidate entre une opération saisie à la main et l'opération importée qui la remplace.
/// </summary>
/// <param name="Manual">Opération saisie à la main.</param>
/// <param name="Imported">Opération importée correspondante.</param>
/// <param name="DayGap">Écart absolu en jours entre les deux dates.</param>
public sealed record ManualEntryMatch(Transaction Manual, Transaction Imported, int DayGap);

/// <summary>
/// Repère les paires opération manuelle / opération importée à fusionner.
/// </summary>
public static class ManualEntryMatchFinder
{
    /// <summary>
    /// Associe chaque opération manuelle à au plus une opération importée, et inversement.
    /// </summary>
    /// <param name="manualTransactions">Opérations saisies à la main, non encore fusionnées.</param>
    /// <param name="importedTransactions">Opérations importées susceptibles de les remplacer.</param>
    /// <param name="toleranceDays">Écart maximal en jours entre les deux dates.</param>
    /// <returns>Les paires retenues, triées par date de saisie.</returns>
    /// <exception cref="ArgumentOutOfRangeException">La tolérance est négative.</exception>
    public static IReadOnlyList<ManualEntryMatch> Find(
        IReadOnlyList<Transaction> manualTransactions,
        IReadOnlyList<Transaction> importedTransactions,
        int toleranceDays)
    {
        ArgumentNullException.ThrowIfNull(manualTransactions);
        ArgumentNullException.ThrowIfNull(importedTransactions);
        ArgumentOutOfRangeException.ThrowIfNegative(toleranceDays);

        // Le libellé saisi à la main ressemble rarement à celui de la banque : il ne sert qu'à départager
        // des candidats, jamais à en écarter. Meilleures paires d'abord, pour qu'une saisie lointaine
        // ne confisque pas l'opération importée d'une saisie plus proche.
        var candidates = (
            from manual in manualTransactions
            from imported in importedTransactions
            where IsCompatible(manual, imported)
            let gap = (int)Math.Abs((imported.Date.Date - manual.Date.Date).TotalDays)
            where gap <= toleranceDays
            orderby gap, SharedWordCount(manual, imported) descending, manual.Date
            select new ManualEntryMatch(manual, imported, gap)).ToList();

        var usedManual = new HashSet<Guid>();
        var usedImported = new HashSet<Guid>();
        var matches = new List<ManualEntryMatch>();

        foreach (var candidate in candidates)
        {
            if (usedManual.Contains(candidate.Manual.Id) || usedImported.Contains(candidate.Imported.Id))
            {
                continue;
            }

            usedManual.Add(candidate.Manual.Id);
            usedImported.Add(candidate.Imported.Id);
            matches.Add(candidate);
        }

        return matches.OrderBy(m => m.Manual.Date).ToList();
    }

    private static bool IsCompatible(Transaction manual, Transaction imported) =>
        manual.BankAccountId == imported.BankAccountId
        && manual.Type == imported.Type
        && manual.Amount == imported.Amount;

    private static int SharedWordCount(Transaction manual, Transaction imported) =>
        StatementLabelAnalyzer.ExtractMeaningfulTokens(manual.Label, manual.RawLabel)
            .Intersect(StatementLabelAnalyzer.ExtractMeaningfulTokens(imported.Label, imported.RawLabel), StringComparer.OrdinalIgnoreCase)
            .Count();
}
