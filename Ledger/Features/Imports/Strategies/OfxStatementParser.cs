using Ledger.Features.Accounts.Entities;
using Ledger.Features.Imports.Entities;
using Ledger.Features.Imports.Models;

namespace Ledger.Features.Imports.Strategies;

/// <summary>
/// Stratégie de parsing pour le format standard bancaire international OFX (Open Financial Exchange).
/// Délègue le traitement technique au framework partagé StatementForge.
/// </summary>
public class OfxStatementParser : IBankStatementParser
{
    private readonly StatementForge.Parsing.OfxStatementParser _underlyingParser = new();

    public string SupportedFormat => _underlyingParser.SupportedFormat;

    /// <summary>
    /// Extrait la valeur de la balise &lt;ACCTID&gt; depuis un fichier ou flux OFX.
    /// </summary>
    public static Task<string?> ExtractOfxAccountIdAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        return StatementForge.Parsing.OfxStatementParser.ExtractOfxAccountIdAsync(stream, cancellationToken);
    }

    public bool CanHandle(string fileName, Stream stream)
    {
        return _underlyingParser.CanHandle(fileName, stream);
    }

    public async Task<IReadOnlyList<ImportedTransactionDto>> ParseAsync(
        Stream stream,
        CsvMappingProfile? mappingProfile,
        CancellationToken cancellationToken = default)
    {
        var statementTransactions = await _underlyingParser.ParseAsync(stream, null, cancellationToken).ConfigureAwait(false);

        return statementTransactions.Select(st => new ImportedTransactionDto
        {
            Date = st.Date,
            Amount = st.Amount,
            Type = st.Type == StatementForge.Models.StatementTransactionType.Debit ? TransactionType.Debit : TransactionType.Credit,
            Label = st.Label,
            RawLabel = st.RawLabel,
            Reference = st.Reference,
            ExternalId = st.ExternalId,
            CategoryName = st.CategoryCandidate
        }).ToList();
    }
}
