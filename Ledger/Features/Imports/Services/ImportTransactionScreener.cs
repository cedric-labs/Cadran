using Ledger.Features.Accounts.Entities;
using Ledger.Features.Imports.Models;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Imports.Services;

/// <summary>
/// Opérations d'un relevé réparties selon le contrôle des doublons.
/// </summary>
/// <param name="NewTransactions">Opérations à enregistrer.</param>
/// <param name="SuspectedDuplicates">Opérations laissées à l'arbitrage de l'utilisateur.</param>
/// <param name="ConfirmedDuplicatesCount">Opérations écartées car leur référence bancaire existe déjà.</param>
internal sealed record ImportScreening(
    List<Transaction> NewTransactions,
    List<SuspectedDuplicateDto> SuspectedDuplicates,
    int ConfirmedDuplicatesCount);

/// <summary>
/// Sépare les opérations d'un relevé en nouvelles opérations, doublons confirmés et doublons suspectés.
/// </summary>
internal sealed class ImportTransactionScreener(
    AppDbContext dbContext,
    IUiSettingsService uiSettingsService,
    ImportResultFactory resultFactory)
{
    // Fenêtre de recherche autour des dates du relevé, au-delà de la tolérance configurable.
    private const int ExistingTransactionsMarginDays = 3;

    public async Task<ImportScreening> ScreenAsync(
        IReadOnlyList<ImportedTransactionDto> parsedDtos,
        Guid bankAccountId,
        CancellationToken cancellationToken)
    {
        var minDate = parsedDtos.Min(p => p.Date).AddDays(-ExistingTransactionsMarginDays);
        var maxDate = parsedDtos.Max(p => p.Date).AddDays(ExistingTransactionsMarginDays);

        var existingTransactions = await dbContext.Transactions
            .Where(t => t.BankAccountId == bankAccountId && t.Date >= minDate && t.Date <= maxDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var categoryIdsByName = await dbContext.Categories
            .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken).ConfigureAwait(false);

        var defaultStatus = await dbContext.TransactionStatuses
            .FirstOrDefaultAsync(s => s.IsDefault, cancellationToken).ConfigureAwait(false);

        var toleranceDays = await uiSettingsService.GetDuplicateToleranceDaysAsync(cancellationToken).ConfigureAwait(false);
        var detector = new ImportDuplicateDetector(existingTransactions, toleranceDays);

        var newTransactions = new List<Transaction>();
        var suspectedDuplicates = new List<SuspectedDuplicateDto>();
        var confirmedDuplicatesCount = 0;

        foreach (var dto in parsedDtos)
        {
            var verdict = detector.ClassifyAndRegister(dto);
            switch (verdict.Kind)
            {
                case ImportDuplicateKind.SameReference:
                    confirmedDuplicatesCount++;
                    break;
                case ImportDuplicateKind.Similar:
                    suspectedDuplicates.Add(ToSuspectedDuplicate(dto, suspectedDuplicates.Count, verdict));
                    break;
                default:
                    newTransactions.Add(ImportedTransactionFactory.Create(dto, bankAccountId, defaultStatus, categoryIdsByName));
                    break;
            }
        }

        return new ImportScreening(newTransactions, suspectedDuplicates, confirmedDuplicatesCount);
    }

    private SuspectedDuplicateDto ToSuspectedDuplicate(ImportedTransactionDto dto, int index, ImportDuplicateVerdict verdict) =>
        new(
            Index: index,
            Date: dto.Date,
            ValueDate: dto.ValueDate,
            Amount: dto.Amount,
            Type: dto.Type,
            Label: dto.Label,
            RawLabel: dto.RawLabel,
            ExistingMatchLabel: resultFactory.DescribeMatch(verdict),
            Reference: dto.Reference,
            CategoryName: dto.CategoryName);
}
