using System.Text.RegularExpressions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Imports.Strategies;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Imports.Services;

/// <summary>
/// Compte cible d'un import, avec l'identifiant OFX lu dans le fichier.
/// </summary>
/// <param name="Account">Compte retenu, ou <c>null</c> si aucun ne correspond.</param>
/// <param name="DetectedOfxAcctId">Valeur de la balise <c>ACCTID</c>, si le fichier est un OFX qui en contient une.</param>
/// <param name="AutoMatchedByOfx"><c>true</c> si le compte a été trouvé grâce à l'identifiant OFX plutôt que par sélection manuelle.</param>
internal sealed record ImportAccountResolution(BankAccount? Account, string? DetectedOfxAcctId, bool AutoMatchedByOfx);

/// <summary>
/// Détermine le compte bancaire qui reçoit un relevé : appariement par identifiant OFX d'abord,
/// compte sélectionné par l'utilisateur ensuite.
/// </summary>
internal sealed class ImportAccountResolver(AppDbContext dbContext)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    public async Task<ImportAccountResolution> ResolveAsync(
        IBankStatementParser parser,
        Stream statement,
        Guid? selectedAccountId,
        CancellationToken cancellationToken)
    {
        string? detectedOfxAcctId = null;
        if (string.Equals(parser.SupportedFormat, "OFX", StringComparison.Ordinal))
        {
            detectedOfxAcctId = await OfxStatementParser.ExtractOfxAccountIdAsync(statement, cancellationToken).ConfigureAwait(false);
            statement.Position = 0;
        }

        if (!string.IsNullOrEmpty(detectedOfxAcctId))
        {
            var matched = await FindByOfxAccountIdAsync(detectedOfxAcctId, cancellationToken).ConfigureAwait(false);
            if (matched is not null)
            {
                return new ImportAccountResolution(matched, detectedOfxAcctId, AutoMatchedByOfx: true);
            }
        }

        if (selectedAccountId is not { } accountId || accountId == Guid.Empty)
        {
            return new ImportAccountResolution(null, detectedOfxAcctId, AutoMatchedByOfx: false);
        }

        var selected = await dbContext.BankAccounts
            .Include(a => a.Bank)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken).ConfigureAwait(false);
        return new ImportAccountResolution(selected, detectedOfxAcctId, AutoMatchedByOfx: false);
    }

    private async Task<BankAccount?> FindByOfxAccountIdAsync(string detectedOfxAcctId, CancellationToken cancellationToken)
    {
        var cleanAcctId = StripSeparators(detectedOfxAcctId);
        var activeAccounts = await dbContext.BankAccounts
            .Include(a => a.Bank)
            .Where(a => !a.IsArchived)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return activeAccounts.FirstOrDefault(a =>
            !string.IsNullOrEmpty(a.OfxAccountId) &&
            (string.Equals(a.OfxAccountId.Trim(), detectedOfxAcctId, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(StripSeparators(a.OfxAccountId), cleanAcctId, StringComparison.OrdinalIgnoreCase)));
    }

    // Les banques formatent le numéro de compte avec des espaces ou des tirets selon l'export.
    private static string StripSeparators(string accountId) =>
        Regex.Replace(accountId, @"[\s\-]", "", RegexOptions.None, RegexTimeout);
}
