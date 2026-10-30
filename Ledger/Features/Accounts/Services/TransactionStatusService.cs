using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Security.Context;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Service gérant les statuts personnalisés d'opérations bancaires.
/// </summary>
public sealed class TransactionStatusService(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<TransactionStatusService> logger) : ITransactionStatusService
{
    private static readonly IReadOnlyDictionary<string, (string En, string Es, string De)> DefaultStatusTranslations =
        new Dictionary<string, (string En, string Es, string De)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Reconciled"] = ("Reconciled", "Conciliada", "Abgeglichen"),
            ["Cleared"] = ("Cleared", "Contabilizada", "Gebucht"),
            ["Pending"] = ("Pending", "Pendiente", "Ausstehend")
        };

    public async Task<List<TransactionCustomStatus>> GetAllStatusesAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId;
        if (!string.IsNullOrEmpty(userId))
        {
            await EnsureDefaultStatusesForUserAsync(userId, cancellationToken);
        }

        await EnsureDefaultTranslationsAsync(cancellationToken);

        return await dbContext.TransactionStatuses
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<TransactionCustomStatus?> GetDefaultStatusAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await GetAllStatusesAsync(cancellationToken);
        return statuses.FirstOrDefault(s => s.IsDefault)
            ?? statuses.FirstOrDefault(s => string.Equals(s.Code, "Cleared", StringComparison.Ordinal))
            ?? statuses.FirstOrDefault();
    }

    public Task<TransactionCustomStatus?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.TransactionStatuses.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<TransactionCustomStatus> CreateAsync(TransactionCustomStatus status, CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? string.Empty;
        status.UserId = userId;

        if (status.IsDefault)
        {
            var otherDefaults = await dbContext.TransactionStatuses
                .Where(s => s.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var s in otherDefaults)
            {
                s.IsDefault = false;
            }
        }

        var maxOrder = await dbContext.TransactionStatuses
            .MaxAsync(s => (int?)s.DisplayOrder, cancellationToken) ?? 0;
        status.DisplayOrder = maxOrder + 1;

        dbContext.TransactionStatuses.Add(status);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Nouveau statut '{StatusName}' créé pour l'utilisateur {UserId}.", status.Name, userId);
        return status;
    }

    public async Task<TransactionCustomStatus> UpdateAsync(TransactionCustomStatus status, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.TransactionStatuses.FirstOrDefaultAsync(s => s.Id == status.Id, cancellationToken);
        if (existing == null)
        {
            throw new KeyNotFoundException("Statut introuvable.");
        }

        existing.Name = status.Name.Trim();
        existing.Description = string.IsNullOrWhiteSpace(status.Description) ? null : status.Description.Trim();
        existing.ColorHex = string.IsNullOrWhiteSpace(status.ColorHex) ? "#3b82f6" : status.ColorHex.Trim();
        existing.IconName = string.IsNullOrWhiteSpace(status.IconName) ? "bi-check-circle" : status.IconName.Trim();

        if (status.IsDefault && !existing.IsDefault)
        {
            var otherDefaults = await dbContext.TransactionStatuses
                .Where(s => s.Id != existing.Id && s.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var s in otherDefaults)
            {
                s.IsDefault = false;
            }
            existing.IsDefault = true;
        }
        else if (!status.IsDefault && existing.IsDefault)
        {
            // Conserver au moins un statut par défaut si possible
            existing.IsDefault = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Statut '{StatusName}' ({StatusId}) mis à jour.", existing.Name, existing.Id);
        return existing;
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var status = await dbContext.TransactionStatuses.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (status == null)
        {
            return (false, "Statut introuvable.");
        }

        if (status.IsSystem)
        {
            return (false, "Les statuts de base (Pointée, Comptabilisée, En attente) ne peuvent pas être supprimés.");
        }

        var usedCount = await dbContext.Transactions.CountAsync(t => t.CustomStatusId == id, cancellationToken);
        if (usedCount > 0)
        {
            return (false, $"Impossible de supprimer ce statut car il est actuellement utilisé par {usedCount} opération(s). Veuillez réassigner ces opérations à un autre statut au préalable.");
        }

        dbContext.TransactionStatuses.Remove(status);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Statut '{StatusName}' ({StatusId}) supprimé.", status.Name, id);
        return (true, null);
    }

    public Task<int> GetTransactionCountForStatusAsync(Guid statusId, CancellationToken cancellationToken = default)
    {
        return dbContext.Transactions.CountAsync(t => t.CustomStatusId == statusId, cancellationToken);
    }

    public async Task EnsureDefaultStatusesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return;

        var hasExisting = await dbContext.TransactionStatuses
            .IgnoreQueryFilters()
            .AnyAsync(s => s.UserId == userId, cancellationToken);
        if (hasExisting) return;

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

        dbContext.TransactionStatuses.AddRange(reconciled, cleared, pending);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Mettre à jour les transactions existantes qui n'ont pas encore de CustomStatusId
        var txWithoutStatus = await dbContext.Transactions
            .Where(t => t.CustomStatusId == null)
            .ToListAsync(cancellationToken);

        if (txWithoutStatus.Count > 0)
        {
            foreach (var t in txWithoutStatus)
            {
                t.CustomStatusId = t.Status switch
                {
                    TransactionStatus.Reconciled => reconciled.Id,
                    TransactionStatus.Pending => pending.Id,
                    _ => cleared.Id
                };
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Statuts par défaut créés et associés pour l'utilisateur {UserId}.", userId);
        await EnsureDefaultTranslationsAsync(cancellationToken);
    }

    private async Task EnsureDefaultTranslationsAsync(CancellationToken cancellationToken)
    {
        var statuses = await dbContext.TransactionStatuses
            .Where(s => s.Code != null && s.Code != "")
            .ToListAsync(cancellationToken);

        if (statuses.Count == 0) return;

        var statusIds = statuses.Select(s => s.Id).ToHashSet();
        var existingTranslations = await dbContext.EntityTranslations
            .Where(t => t.EntityType == "TransactionCustomStatus" && statusIds.Contains(t.EntityId) && t.Field == "Name")
            .ToListAsync(cancellationToken);

        var existingKeySet = existingTranslations
            .Select(t => $"{t.EntityId}:{t.LanguageCode.ToLowerInvariant()}")
            .ToHashSet(StringComparer.Ordinal);

        var toAdd = new List<EntityTranslation>();
        foreach (var status in statuses)
        {
            if (string.IsNullOrEmpty(status.Code) || !DefaultStatusTranslations.TryGetValue(status.Code, out var trans))
            {
                continue;
            }

            if (!existingKeySet.Contains($"{status.Id}:en") && !string.IsNullOrWhiteSpace(trans.En))
            {
                toAdd.Add(new EntityTranslation
                {
                    EntityType = "TransactionCustomStatus",
                    EntityId = status.Id,
                    Field = "Name",
                    LanguageCode = "en",
                    Translation = trans.En
                });
            }

            if (!existingKeySet.Contains($"{status.Id}:es") && !string.IsNullOrWhiteSpace(trans.Es))
            {
                toAdd.Add(new EntityTranslation
                {
                    EntityType = "TransactionCustomStatus",
                    EntityId = status.Id,
                    Field = "Name",
                    LanguageCode = "es",
                    Translation = trans.Es
                });
            }

            if (!existingKeySet.Contains($"{status.Id}:de") && !string.IsNullOrWhiteSpace(trans.De))
            {
                toAdd.Add(new EntityTranslation
                {
                    EntityType = "TransactionCustomStatus",
                    EntityId = status.Id,
                    Field = "Name",
                    LanguageCode = "de",
                    Translation = trans.De
                });
            }
        }

        if (toAdd.Count > 0)
        {
            dbContext.EntityTranslations.AddRange(toAdd);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Ajout de {Count} traductions de statuts par défaut (EN, ES, DE).", toAdd.Count);
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "Concurrence lors de l'insertion des traductions par défaut des statuts.");
            }
        }
    }

    public async Task<int> DeduplicateStatusesAsync(CancellationToken cancellationToken = default)
    {
        var allStatuses = await dbContext.TransactionStatuses
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        if (allStatuses.Count == 0)
        {
            return 0;
        }

        var usedStatusIdsList = await dbContext.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.CustomStatusId != null)
            .Select(t => t.CustomStatusId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var usedStatusIds = usedStatusIdsList
            .Where(id => id.HasValue)
            .Select(id => id.GetValueOrDefault())
            .ToHashSet();

        var toDelete = new List<TransactionCustomStatus>();

        var groups = allStatuses.GroupBy(s => (
            UserId: s.UserId,
            Key: !string.IsNullOrWhiteSpace(s.Code)
                ? $"CODE:{s.Code.Trim().ToUpperInvariant()}"
                : $"NAME:{s.Name.Trim().ToUpperInvariant()}"
        ));

        foreach (var group in groups)
        {
            var groupList = group.ToList();
            if (groupList.Count <= 1)
            {
                continue;
            }

            var usedInGroup = groupList.Where(s => usedStatusIds.Contains(s.Id)).ToList();
            var unusedInGroup = groupList.Where(s => !usedStatusIds.Contains(s.Id)).ToList();

            if (usedInGroup.Count > 0)
            {
                toDelete.AddRange(unusedInGroup);
            }
            else
            {
                var canonical = unusedInGroup
                    .OrderBy(s => s.CreatedAtUtc)
                    .ThenBy(s => s.Id)
                    .First();

                toDelete.AddRange(unusedInGroup.Where(s => s.Id != canonical.Id));
            }
        }

        if (toDelete.Count == 0)
        {
            return 0;
        }

        var deletedIds = toDelete.Select(s => s.Id).ToHashSet();
        var orphanTranslations = await dbContext.EntityTranslations
            .Where(t => t.EntityType == "TransactionCustomStatus" && deletedIds.Contains(t.EntityId))
            .ToListAsync(cancellationToken);

        if (orphanTranslations.Count > 0)
        {
            dbContext.EntityTranslations.RemoveRange(orphanTranslations);
        }

        dbContext.TransactionStatuses.RemoveRange(toDelete);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // En cas d'exécution concurrente (ex: démarrage simultané d'hôtes de tests E2E),
            // les doublons ont déjà été purgés par l'autre instance.
            logger.LogWarning(ex, "Concurrence détectée lors de la suppression des doublons de statuts d'opérations.");
            return 0;
        }

        logger.LogInformation("Nettoyage des statuts d'opérations terminé : {DeletedCount} doublon(s) non utilisé(s) supprimé(s).", toDelete.Count);
        return toDelete.Count;
    }
}
