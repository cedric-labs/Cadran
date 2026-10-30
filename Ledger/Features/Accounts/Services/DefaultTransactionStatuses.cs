using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Les trois statuts système créés pour chaque utilisateur : pointée, comptabilisée (par défaut) et en attente.
/// </summary>
internal sealed record DefaultTransactionStatuses(
    TransactionCustomStatus Reconciled,
    TransactionCustomStatus Cleared,
    TransactionCustomStatus Pending)
{
    /// <summary>Les statuts dans leur ordre d'affichage.</summary>
    public IReadOnlyList<TransactionCustomStatus> All => [Reconciled, Cleared, Pending];

    /// <summary>
    /// Crée les statuts système de l'utilisateur.
    /// </summary>
    public static DefaultTransactionStatuses CreateFor(string userId)
    {
        var reconciled = new TransactionCustomStatus
        {
            UserId = userId,
            Name = "Pointée",
            Code = "Reconciled",
            Description = "Opération vérifiée et validée avec le relevé bancaire officiel",
            ColorHex = "#198754",
            IconName = "bi-check2-all",
            IsSystem = true,
            IsDefault = false,
            DisplayOrder = 1
        };

        var cleared = new TransactionCustomStatus
        {
            UserId = userId,
            Name = "Comptabilisée",
            Code = "Cleared",
            Description = "Opération passée sur le compte bancaire",
            ColorHex = "#0dcaf0",
            IconName = "bi-check-lg",
            IsSystem = true,
            IsDefault = true,
            DisplayOrder = 2
        };

        var pending = new TransactionCustomStatus
        {
            UserId = userId,
            Name = "En attente",
            Code = "Pending",
            Description = "Opération engagée non encore passée en banque (chèque non débité, paiement en attente)",
            ColorHex = "#ffc107",
            IconName = "bi-hourglass-split",
            IsSystem = true,
            IsDefault = false,
            DisplayOrder = 3
        };

        return new DefaultTransactionStatuses(reconciled, cleared, pending);
    }

    /// <summary>
    /// Retourne le statut personnalisé équivalent à l'ancien statut énuméré d'une opération.
    /// </summary>
    public TransactionCustomStatus ForLegacyStatus(TransactionStatus status)
    {
        return status switch
        {
            TransactionStatus.Reconciled => Reconciled,
            TransactionStatus.Pending => Pending,
            _ => Cleared
        };
    }
}
