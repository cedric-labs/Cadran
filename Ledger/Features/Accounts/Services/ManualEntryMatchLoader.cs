using Ledger.Features.Accounts.Entities;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Charge les opérations manuelles en attente et les opérations importées qui peuvent les remplacer,
/// puis en tire les paires à proposer à la fusion.
/// </summary>
public sealed class ManualEntryMatchLoader(AppDbContext dbContext, IUiSettingsService uiSettingsService)
{
    /// <summary>
    /// Renvoie les paires opération manuelle / opération importée, avec la tolérance de dates des doublons.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Les paires retenues, triées par date de saisie.</returns>
    public async Task<IReadOnlyList<ManualEntryMatch>> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var manual = await dbContext.Transactions
            .AsNoTracking()
            .Include(t => t.BankAccount)
            .Where(t => t.Origin == TransactionOrigin.Manual)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (manual.Count == 0)
        {
            return [];
        }

        var toleranceDays = await uiSettingsService.GetDuplicateToleranceDaysAsync(cancellationToken).ConfigureAwait(false);
        var accountIds = manual.Select(t => t.BankAccountId).Distinct().ToList();
        var from = manual.Min(t => t.Date).AddDays(-toleranceDays);
        var to = manual.Max(t => t.Date).AddDays(toleranceDays);

        var imported = await dbContext.Transactions
            .AsNoTracking()
            .Include(t => t.BankAccount)
            .Where(t => t.Origin == TransactionOrigin.Import
                && accountIds.Contains(t.BankAccountId)
                && t.Date >= from
                && t.Date <= to)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ManualEntryMatchFinder.Find(manual, imported, toleranceDays);
    }
}
