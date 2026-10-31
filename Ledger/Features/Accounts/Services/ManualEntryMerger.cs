using Ledger.Features.Accounts.Entities;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Issue d'une fusion entre une opération manuelle et une opération importée.
/// </summary>
public enum ManualEntryMergeResult
{
    Merged = 0,
    NotFound,
    NotManual,
    NotImported,
    Incompatible
}

/// <summary>
/// Fusionne une opération saisie à la main dans l'opération importée qui la remplace :
/// l'importée prévaut, la saisie ne lui apporte que ce qui lui manque, puis elle est supprimée.
/// </summary>
public sealed class ManualEntryMerger(AppDbContext dbContext)
{
    /// <summary>
    /// Fusionne l'opération manuelle dans l'opération importée.
    /// </summary>
    /// <param name="manualId">Identifiant de l'opération saisie à la main, supprimée en cas de succès.</param>
    /// <param name="importedId">Identifiant de l'opération importée, conservée et complétée.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns><see cref="ManualEntryMergeResult.Merged"/>, ou le motif du refus (rien n'est alors modifié).</returns>
    public async Task<ManualEntryMergeResult> MergeAsync(Guid manualId, Guid importedId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var manual = await dbContext.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == manualId, cancellationToken)
            .ConfigureAwait(false);
        var imported = await dbContext.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == importedId, cancellationToken)
            .ConfigureAwait(false);

        var refusal = Check(manual, imported);
        if (refusal != ManualEntryMergeResult.Merged || manual is null || imported is null)
        {
            return refusal;
        }

        CompleteEmptyFields(manual, imported);
        MoveOrDropSplits(manual, imported);

        dbContext.Transactions.Remove(manual);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ManualEntryMergeResult.Merged;
    }

    private static ManualEntryMergeResult Check(Transaction? manual, Transaction? imported)
    {
        if (manual is null || imported is null)
        {
            return ManualEntryMergeResult.NotFound;
        }

        if (manual.Origin != TransactionOrigin.Manual)
        {
            return ManualEntryMergeResult.NotManual;
        }

        if (imported.Origin != TransactionOrigin.Import)
        {
            return ManualEntryMergeResult.NotImported;
        }

        var compatible = manual.BankAccountId == imported.BankAccountId
            && manual.Type == imported.Type
            && manual.Amount == imported.Amount;
        return compatible ? ManualEntryMergeResult.Merged : ManualEntryMergeResult.Incompatible;
    }

    private static void CompleteEmptyFields(Transaction manual, Transaction imported)
    {
        imported.CategoryId ??= manual.CategoryId;
        imported.MerchantId ??= manual.MerchantId;
        imported.BudgetDate ??= manual.BudgetDate;
        imported.CustomStatusId ??= manual.CustomStatusId;

        if (string.IsNullOrWhiteSpace(imported.Notes))
        {
            imported.Notes = manual.Notes;
        }
    }

    // Deux ventilations d'une même opération n'ont pas de sens : celle de la banque prévaut, comme le reste.
    private void MoveOrDropSplits(Transaction manual, Transaction imported)
    {
        if (manual.Splits.Count == 0)
        {
            return;
        }

        if (imported.Splits.Count > 0)
        {
            dbContext.TransactionSplits.RemoveRange(manual.Splits);
            return;
        }

        foreach (var split in manual.Splits.ToList())
        {
            split.TransactionId = imported.Id;
        }

        imported.IsSplit = true;
    }
}
