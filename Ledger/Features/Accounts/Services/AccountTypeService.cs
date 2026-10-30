using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Security.Context;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Service gérant les types de comptes bancaires personnalisés de l'utilisateur.
/// </summary>
public sealed class AccountTypeService(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<AccountTypeService> logger) : IAccountTypeService
{
    public async Task<List<CustomAccountType>> GetAllAccountTypesAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId;
        if (!string.IsNullOrEmpty(userId))
        {
            await EnsureDefaultAccountTypesForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        await EnsureDefaultTranslationsAsync(cancellationToken).ConfigureAwait(false);

        return await dbContext.CustomAccountTypes
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<CustomAccountType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.CustomAccountTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<CustomAccountType> CreateAsync(CustomAccountType accountType, CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? string.Empty;
        accountType.UserId = userId;

        var maxOrder = await dbContext.CustomAccountTypes
            .MaxAsync(t => (int?)t.DisplayOrder, cancellationToken).ConfigureAwait(false) ?? 0;
        accountType.DisplayOrder = maxOrder + 1;

        dbContext.CustomAccountTypes.Add(accountType);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return accountType;
    }

    public async Task<CustomAccountType> UpdateAsync(CustomAccountType accountType, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.CustomAccountTypes
            .FirstOrDefaultAsync(t => t.Id == accountType.Id, cancellationToken)
.ConfigureAwait(false) ?? throw new KeyNotFoundException($"Type de compte {accountType.Id} non trouvé.");

        existing.Name = accountType.Name;
        existing.Code = accountType.Code;
        existing.Description = accountType.Description;
        existing.CategoryGroup = accountType.CategoryGroup;
        existing.ColorHex = accountType.ColorHex;
        existing.IconName = accountType.IconName;
        existing.DisplayOrder = accountType.DisplayOrder;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing;
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.CustomAccountTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);
        if (item == null)
        {
            return (false, "Type de compte introuvable.");
        }

        if (item.IsSystem)
        {
            return (false, "Les types de comptes natifs du système ne peuvent pas être supprimés.");
        }

        var usedInAccounts = await dbContext.BankAccounts.AnyAsync(a => a.CustomAccountTypeId == id, cancellationToken).ConfigureAwait(false);
        if (usedInAccounts)
        {
            return (false, "Ce type de compte est actuellement associé à un ou plusieurs comptes bancaires. Modifiez d'abord ces comptes avant de supprimer ce type.");
        }

        dbContext.CustomAccountTypes.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (true, null);
    }

    public async Task EnsureDefaultAccountTypesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return;

        var hasExisting = await dbContext.CustomAccountTypes
            .IgnoreQueryFilters()
            .AnyAsync(t => t.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (hasExisting) return;

        var defaults = DefaultAccountTypeCatalog.CreateFor(userId);

        dbContext.CustomAccountTypes.AddRange(defaults);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Mettre à jour les comptes existants qui n'ont pas encore de CustomAccountTypeId
        var accountsWithoutType = await dbContext.BankAccounts
            .Where(a => a.CustomAccountTypeId == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (accountsWithoutType.Count > 0)
        {
            var defaultMap = defaults.ToDictionary(d => d.Code ?? string.Empty, d => d.Id, StringComparer.Ordinal);
            foreach (var acc in accountsWithoutType)
            {
                var code = acc.Type.ToString();
                if (defaultMap.TryGetValue(code, out var typeId))
                {
                    acc.CustomAccountTypeId = typeId;
                }
                else if (defaultMap.TryGetValue("Checking", out var chkId))
                {
                    acc.CustomAccountTypeId = chkId;
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Types de comptes français par défaut initialisés en base pour l'utilisateur {UserId}.", userId);
        await EnsureDefaultTranslationsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureDefaultTranslationsAsync(CancellationToken cancellationToken)
    {
        var types = await dbContext.CustomAccountTypes
            .Where(t => t.Code != null && t.Code != "")
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (types.Count == 0) return;

        var typeIds = types.Select(t => t.Id).ToHashSet();
        var existingTranslations = await dbContext.EntityTranslations
            .Where(t => t.EntityType == "CustomAccountType" && typeIds.Contains(t.EntityId) && t.Field == "Name")
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var existingKeySet = existingTranslations
            .Select(t => $"{t.EntityId}:{t.LanguageCode.ToLowerInvariant()}")
            .ToHashSet(StringComparer.Ordinal);

        var toAdd = AccountTypeTranslationPlanner.PlanMissing(types, existingKeySet);

        if (toAdd.Count > 0)
        {
            dbContext.EntityTranslations.AddRange(toAdd);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Ajout de {Count} traductions de types de comptes par défaut (EN, ES, DE).", toAdd.Count);
            }
            catch (DbUpdateException ex)
            {
                // Tolérer si les traductions ont déjà été insérées concurremment par un autre thread/processus
                logger.LogWarning(ex, "Concurrence lors de l'insertion des traductions par défaut des types de comptes.");
            }
        }
    }

    public async Task<int> DeduplicateAccountTypesAsync(CancellationToken cancellationToken = default)
    {
        var allTypes = await dbContext.CustomAccountTypes
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (allTypes.Count == 0)
        {
            return 0;
        }

        // 1. Identifiants des types de comptes activement utilisés par au moins un compte bancaire
        var usedTypeIdsList = await dbContext.BankAccounts
            .IgnoreQueryFilters()
            .Where(a => a.CustomAccountTypeId != null)
            .Select(a => a.CustomAccountTypeId)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var usedTypeIds = usedTypeIdsList
            .Where(id => id.HasValue)
            .Select(id => id.GetValueOrDefault())
            .ToHashSet();

        // 2. Rattachement préalable des comptes sans type au type approprié
        var accountsWithoutType = await dbContext.BankAccounts
            .IgnoreQueryFilters()
            .Where(a => a.CustomAccountTypeId == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (accountsWithoutType.Count > 0)
        {
            UntypedAccountLinker.LinkToMatchingTypes(accountsWithoutType, allTypes, usedTypeIds);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // En cas d'initialisation concurrente, les comptes ont déjà été mis à jour par l'autre processus
                logger.LogWarning(ex, "Concurrence détectée lors du rattachement des types aux comptes sans type.");
            }
        }

        var toDelete = DuplicateAccountTypeSelector.SelectRemovable(allTypes, usedTypeIds);

        if (toDelete.Count == 0)
        {
            return 0;
        }

        // 4. Suppression des traductions orphelines associées aux doublons supprimés
        var deletedIds = toDelete.Select(t => t.Id).ToHashSet();
        var orphanTranslations = await dbContext.EntityTranslations
            .Where(t => t.EntityType == "CustomAccountType" && deletedIds.Contains(t.EntityId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (orphanTranslations.Count > 0)
        {
            dbContext.EntityTranslations.RemoveRange(orphanTranslations);
        }

        dbContext.CustomAccountTypes.RemoveRange(toDelete);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // En cas d'exécution concurrente (ex: démarrage simultané d'hôtes de tests E2E),
            // les doublons ont déjà été purgés par l'autre instance.
            logger.LogWarning(ex, "Concurrence détectée lors de la suppression des doublons de types de comptes.");
            return 0;
        }

        logger.LogInformation("Nettoyage des types de comptes terminé : {DeletedCount} doublon(s) non utilisé(s) supprimé(s).", toDelete.Count);
        return toDelete.Count;
    }
}
