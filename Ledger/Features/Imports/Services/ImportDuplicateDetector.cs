using Ledger.Features.Accounts.Entities;
using Ledger.Features.Imports.Models;

namespace Ledger.Features.Imports.Services;

internal enum ImportDuplicateKind
{
    None,
    SameReference,
    Similar
}

/// <summary>
/// Opération déjà acceptée plus tôt dans le fichier en cours d'import.
/// </summary>
internal sealed record AcceptedImportRow(DateTime Date, decimal Amount, TransactionType Type, string Label, HashSet<string> Tokens);

/// <summary>
/// Verdict du contrôle d'une opération importée ; au plus une des deux correspondances est renseignée.
/// </summary>
internal sealed record ImportDuplicateVerdict(
    ImportDuplicateKind Kind,
    Transaction? ExistingMatch = null,
    AcceptedImportRow? BatchMatch = null)
{
    public static ImportDuplicateVerdict NotDuplicate { get; } = new(ImportDuplicateKind.None);

    public static ImportDuplicateVerdict SameReference { get; } = new(ImportDuplicateKind.SameReference);
}

/// <summary>
/// Compare chaque opération importée aux opérations déjà en base et à celles acceptées plus tôt dans le même fichier.
/// </summary>
internal sealed class ImportDuplicateDetector(IReadOnlyList<Transaction> existingTransactions, int toleranceDays)
{
    private readonly HashSet<string> _acceptedReferences = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AcceptedImportRow> _acceptedRows = [];

    /// <summary>
    /// Classe l'opération et, si elle n'est pas un doublon, la mémorise pour les comparaisons suivantes du même fichier.
    /// </summary>
    public ImportDuplicateVerdict ClassifyAndRegister(ImportedTransactionDto dto)
    {
        if (IsSameReference(dto))
        {
            return ImportDuplicateVerdict.SameReference;
        }

        var tokens = StatementForge.Analysis.StatementLabelAnalyzer.ExtractMeaningfulTokens(dto.Label, dto.RawLabel);
        var existingMatch = FindExistingMatch(dto, tokens);
        var batchMatch = FindBatchMatch(dto, tokens);

        if (existingMatch is not null || batchMatch is not null)
        {
            return new ImportDuplicateVerdict(ImportDuplicateKind.Similar, existingMatch, batchMatch);
        }

        if (!string.IsNullOrEmpty(dto.Reference))
        {
            _acceptedReferences.Add(dto.Reference);
        }

        _acceptedRows.Add(new AcceptedImportRow(dto.Date, dto.Amount, dto.Type, dto.Label, tokens));
        return ImportDuplicateVerdict.NotDuplicate;
    }

    // Une même référence bancaire (FITID) identifie la même opération : fichier réimporté ou export de la même banque.
    private bool IsSameReference(ImportedTransactionDto dto) =>
        !string.IsNullOrEmpty(dto.Reference)
        && (existingTransactions.Any(t => string.Equals(t.Reference, dto.Reference, StringComparison.OrdinalIgnoreCase))
            || _acceptedReferences.Contains(dto.Reference));

    private Transaction? FindExistingMatch(ImportedTransactionDto dto, HashSet<string> tokens) =>
        existingTransactions.FirstOrDefault(t =>
            t.Amount == dto.Amount &&
            t.Type == dto.Type &&
            IsWithinTolerance(t.Date, dto.Date) &&
            StatementForge.Analysis.StatementLabelAnalyzer.IsLabelMatching(tokens, t.Label, t.RawLabel, dto.Date.Date == t.Date.Date));

    private AcceptedImportRow? FindBatchMatch(ImportedTransactionDto dto, HashSet<string> tokens) =>
        _acceptedRows.FirstOrDefault(r =>
            r.Amount == dto.Amount &&
            r.Type == dto.Type &&
            IsWithinTolerance(r.Date, dto.Date) &&
            StatementForge.Analysis.StatementLabelAnalyzer.IsTokenSetMatching(tokens, r.Tokens, dto.Date.Date == r.Date.Date));

    private bool IsWithinTolerance(DateTime first, DateTime second) =>
        Math.Abs((first.Date - second.Date).TotalDays) <= toleranceDays;
}
