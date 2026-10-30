using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Applique à une transaction le statut choisi par l'utilisateur : statut personnalisé en priorité, sinon statut système.
/// </summary>
public sealed class TransactionStatusApplier(ITransactionStatusService statusService)
{
    public async Task ApplyAsync(
        Transaction transaction,
        Guid? customStatusId,
        TransactionStatus? status,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (customStatusId.HasValue && customStatusId.Value != Guid.Empty)
        {
            var customStatus = await statusService.GetByIdAsync(customStatusId.Value, cancellationToken).ConfigureAwait(false);
            if (customStatus is null)
            {
                return;
            }

            transaction.CustomStatusId = customStatus.Id;
            transaction.CustomStatus = customStatus;

            // Le code d'un statut personnalisé peut désigner un statut système : on aligne alors l'énumération pour que
            // les filtres et totaux fondés sur le statut système restent cohérents.
            if (!string.IsNullOrEmpty(customStatus.Code) && Enum.TryParse<TransactionStatus>(customStatus.Code, out var systemStatus))
            {
                transaction.Status = systemStatus;
            }

            return;
        }

        if (status.HasValue)
        {
            transaction.Status = status.Value;
        }
    }
}
