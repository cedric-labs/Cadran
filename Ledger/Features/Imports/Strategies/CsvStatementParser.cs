using Ledger.Features.Accounts.Entities;
using Ledger.Features.Imports.Entities;
using Ledger.Features.Imports.Models;
using StatementForge.Models;

namespace Ledger.Features.Imports.Strategies;

/// <summary>
/// Stratégie de parsing pour les fichiers tabulaires CSV.
/// Délègue le traitement technique au framework partagé StatementForge.
/// </summary>
public class CsvStatementParser : IBankStatementParser
{
    private readonly StatementForge.Parsing.CsvStatementParser _underlyingParser = new();

    public string SupportedFormat => _underlyingParser.SupportedFormat;

    public bool CanHandle(string fileName, Stream stream)
    {
        return _underlyingParser.CanHandle(fileName, stream);
    }

    public async Task<IReadOnlyList<ImportedTransactionDto>> ParseAsync(
        Stream stream,
        CsvMappingProfile? mappingProfile,
        CancellationToken cancellationToken = default)
    {
        var options = mappingProfile != null
            ? new CsvStatementOptions
            {
                Delimiter = mappingProfile.Delimiter,
                DecimalSeparator = mappingProfile.DecimalSeparator,
                DateFormat = mappingProfile.DateFormat,
                HasHeader = mappingProfile.HasHeader,
                DateColumnIndex = mappingProfile.DateColumnIndex,
                LabelColumnIndex = mappingProfile.LabelColumnIndex,
                AmountColumnIndex = mappingProfile.AmountColumnIndex,
                DebitColumnIndex = mappingProfile.DebitColumnIndex,
                CreditColumnIndex = mappingProfile.CreditColumnIndex,
                ReferenceColumnIndex = mappingProfile.ReferenceColumnIndex
            }
            : new CsvStatementOptions();

        var statementTransactions = await _underlyingParser.ParseAsync(stream, options, cancellationToken).ConfigureAwait(false);

        return statementTransactions.Select(st => new ImportedTransactionDto
        {
            Date = st.Date,
            Amount = st.Amount,
            Type = st.Type == StatementTransactionType.Debit ? TransactionType.Debit : TransactionType.Credit,
            Label = st.Label,
            RawLabel = st.RawLabel,
            Reference = st.Reference,
            ExternalId = st.ExternalId,
            CategoryName = st.CategoryCandidate
        }).ToList();
    }
}
