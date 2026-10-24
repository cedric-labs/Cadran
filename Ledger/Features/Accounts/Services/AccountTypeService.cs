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
            await EnsureDefaultAccountTypesForUserAsync(userId, cancellationToken);
        }

        return await dbContext.CustomAccountTypes
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
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
            .MaxAsync(t => (int?)t.DisplayOrder, cancellationToken) ?? 0;
        accountType.DisplayOrder = maxOrder + 1;

        dbContext.CustomAccountTypes.Add(accountType);
        await dbContext.SaveChangesAsync(cancellationToken);

        return accountType;
    }

    public async Task<CustomAccountType> UpdateAsync(CustomAccountType accountType, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.CustomAccountTypes
            .FirstOrDefaultAsync(t => t.Id == accountType.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Type de compte {accountType.Id} non trouvé.");

        existing.Name = accountType.Name;
        existing.Code = accountType.Code;
        existing.Description = accountType.Description;
        existing.CategoryGroup = accountType.CategoryGroup;
        existing.ColorHex = accountType.ColorHex;
        existing.IconName = accountType.IconName;
        existing.DisplayOrder = accountType.DisplayOrder;

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.CustomAccountTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (item == null)
        {
            return (false, "Type de compte introuvable.");
        }

        if (item.IsSystem)
        {
            return (false, "Les types de comptes natifs du système ne peuvent pas être supprimés.");
        }

        var usedInAccounts = await dbContext.BankAccounts.AnyAsync(a => a.CustomAccountTypeId == id, cancellationToken);
        if (usedInAccounts)
        {
            return (false, "Ce type de compte est actuellement associé à un ou plusieurs comptes bancaires. Modifiez d'abord ces comptes avant de supprimer ce type.");
        }

        dbContext.CustomAccountTypes.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task EnsureDefaultAccountTypesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return;

        var hasExisting = await dbContext.CustomAccountTypes
            .IgnoreQueryFilters()
            .AnyAsync(t => t.UserId == userId, cancellationToken);
        if (hasExisting) return;

        var defaults = new List<CustomAccountType>
        {
            // Liquidités
            new() { UserId = userId, Name = "Compte de Dépôt", Code = "Checking", Description = "Compte courant pour la gestion quotidienne", CategoryGroup = AccountCategoryGroup.Liquid, ColorHex = "#3b82f6", IconName = "bi-wallet2", IsSystem = true, DisplayOrder = 1 },
            new() { UserId = userId, Name = "Carte à débit différé / Crédit", Code = "CreditCard", Description = "Compte carte à débit différé ou carte de crédit", CategoryGroup = AccountCategoryGroup.Liquid, ColorHex = "#ffc107", IconName = "bi-credit-card", IsSystem = true, DisplayOrder = 2 },
            new() { UserId = userId, Name = "Portefeuille / Espèces", Code = "Cash", Description = "Espèces et argent liquide", CategoryGroup = AccountCategoryGroup.Liquid, ColorHex = "#6c757d", IconName = "bi-cash-stack", IsSystem = true, DisplayOrder = 3 },

            // Épargne réglementée
            new() { UserId = userId, Name = "Livret A", Code = "LivretA", Description = "Livret d'épargne réglementée défiscalisé", CategoryGroup = AccountCategoryGroup.Savings, ColorHex = "#0dcaf0", IconName = "bi-piggy-bank", IsSystem = true, DisplayOrder = 4 },
            new() { UserId = userId, Name = "LDD Solidaire", Code = "Ldds", Description = "Livret de développement durable et solidaire", CategoryGroup = AccountCategoryGroup.Savings, ColorHex = "#0dcaf0", IconName = "bi-tree", IsSystem = true, DisplayOrder = 5 },
            new() { UserId = userId, Name = "Livret d'Épargne Populaire (LEP)", Code = "Lep", Description = "Livret d'épargne populaire sous conditions de ressources", CategoryGroup = AccountCategoryGroup.Savings, ColorHex = "#0dcaf0", IconName = "bi-heart", IsSystem = true, DisplayOrder = 6 },
            new() { UserId = userId, Name = "Livret Jeune", Code = "LivretJeune", Description = "Livret réservé aux 12-25 ans", CategoryGroup = AccountCategoryGroup.Savings, ColorHex = "#0dcaf0", IconName = "bi-person", IsSystem = true, DisplayOrder = 7 },

            // Épargne logement & bancaire
            new() { UserId = userId, Name = "Livret bancaire (CSL)", Code = "Savings", Description = "Compte sur livret bancaire ordinaire fiscalisé", CategoryGroup = AccountCategoryGroup.Savings, ColorHex = "#0dcaf0", IconName = "bi-safe", IsSystem = true, DisplayOrder = 8 },
            new() { UserId = userId, Name = "Carré mauve (PEL)", Code = "Pel", Description = "Plan Épargne Logement (Carré Mauve au Crédit Agricole)", CategoryGroup = AccountCategoryGroup.Savings, ColorHex = "#6f42c1", IconName = "bi-house-door", IsSystem = true, DisplayOrder = 9 },
            new() { UserId = userId, Name = "Compte Épargne Logement (CEL)", Code = "Cel", Description = "Compte Épargne Logement", CategoryGroup = AccountCategoryGroup.Savings, ColorHex = "#6f42c1", IconName = "bi-house", IsSystem = true, DisplayOrder = 10 },
            new() { UserId = userId, Name = "Compte à terme (CAT)", Code = "Cat", Description = "Placement à terme à taux fixé", CategoryGroup = AccountCategoryGroup.Savings, ColorHex = "#20c997", IconName = "bi-clock-history", IsSystem = true, DisplayOrder = 11 },

            // Bourse & Titres
            new() { UserId = userId, Name = "Plan d'Épargne en Actions (PEA)", Code = "Pea", Description = "PEA bancaire ou PEA-PME pour actions européennes", CategoryGroup = AccountCategoryGroup.Investment, ColorHex = "#198754", IconName = "bi-graph-up-arrow", IsSystem = true, DisplayOrder = 12 },
            new() { UserId = userId, Name = "Compte-Titres Ordinaire (CTO)", Code = "Investment", Description = "Compte titres pour actions, obligations et ETF internationaux", CategoryGroup = AccountCategoryGroup.Investment, ColorHex = "#198754", IconName = "bi-pie-chart", IsSystem = true, DisplayOrder = 13 },
            new() { UserId = userId, Name = "Compte Parts Sociales", Code = "PartsSociales", Description = "Parts sociales en banque mutualiste / coopérative", CategoryGroup = AccountCategoryGroup.Investment, ColorHex = "#198754", IconName = "bi-building-fill", IsSystem = true, DisplayOrder = 14 },
            new() { UserId = userId, Name = "Assurance-Vie", Code = "AssuranceVie", Description = "Contrat d'assurance-vie ou de capitalisation", CategoryGroup = AccountCategoryGroup.Investment, ColorHex = "#198754", IconName = "bi-shield-check", IsSystem = true, DisplayOrder = 15 },
            new() { UserId = userId, Name = "Plan d'Épargne Retraite (PER)", Code = "Per", Description = "Épargne pour la retraite", CategoryGroup = AccountCategoryGroup.Investment, ColorHex = "#198754", IconName = "bi-umbrella", IsSystem = true, DisplayOrder = 16 },
            new() { UserId = userId, Name = "Crypto-monnaies / Autres actifs", Code = "Crypto", Description = "Comptes d'actifs numériques, métaux ou divers", CategoryGroup = AccountCategoryGroup.Investment, ColorHex = "#fd7e14", IconName = "bi-currency-bitcoin", IsSystem = true, DisplayOrder = 17 }
        };

        dbContext.CustomAccountTypes.AddRange(defaults);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Mettre à jour les comptes existants qui n'ont pas encore de CustomAccountTypeId
        var accountsWithoutType = await dbContext.BankAccounts
            .Where(a => a.CustomAccountTypeId == null)
            .ToListAsync(cancellationToken);

        if (accountsWithoutType.Count > 0)
        {
            var defaultMap = defaults.ToDictionary(d => d.Code ?? string.Empty, d => d.Id);
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
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Types de comptes français par défaut initialisés en base pour l'utilisateur {UserId}.", userId);
    }

    public async Task<int> DeduplicateAccountTypesAsync(CancellationToken cancellationToken = default)
    {
        var allTypes = await dbContext.CustomAccountTypes
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

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
            .ToListAsync(cancellationToken);

        var usedTypeIds = usedTypeIdsList
            .Where(id => id.HasValue)
            .Select(id => id.GetValueOrDefault())
            .ToHashSet();

        // 2. Rattachement préalable des comptes sans type au type approprié
        var accountsWithoutType = await dbContext.BankAccounts
            .IgnoreQueryFilters()
            .Where(a => a.CustomAccountTypeId == null)
            .ToListAsync(cancellationToken);

        if (accountsWithoutType.Count > 0)
        {
            foreach (var acc in accountsWithoutType)
            {
                var code = acc.Type.ToString();
                var matchingType = allTypes.FirstOrDefault(t => t.UserId == acc.UserId && string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase))
                    ?? allTypes.FirstOrDefault(t => t.UserId == acc.UserId && string.Equals(t.Code, "Checking", StringComparison.OrdinalIgnoreCase));

                if (matchingType != null)
                {
                    acc.CustomAccountTypeId = matchingType.Id;
                    usedTypeIds.Add(matchingType.Id);
                }
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // En cas d'initialisation concurrente, les comptes ont déjà été mis à jour par l'autre processus
                logger.LogWarning(ex, "Concurrence détectée lors du rattachement des types aux comptes sans type.");
            }
        }

        var toDelete = new List<CustomAccountType>();

        // 3. Regroupement par utilisateur et clé d'identification (Code prioritaire, sinon Nom)
        var groups = allTypes.GroupBy(t => (
            UserId: t.UserId,
            Key: !string.IsNullOrWhiteSpace(t.Code)
                ? $"CODE:{t.Code.Trim().ToUpperInvariant()}"
                : $"NAME:{t.Name.Trim().ToUpperInvariant()}"
        ));

        foreach (var group in groups)
        {
            var groupList = group.ToList();
            if (groupList.Count <= 1)
            {
                continue;
            }

            var usedInGroup = groupList.Where(t => usedTypeIds.Contains(t.Id)).ToList();
            var unusedInGroup = groupList.Where(t => !usedTypeIds.Contains(t.Id)).ToList();

            if (usedInGroup.Count > 0)
            {
                // Règle : Ne pas supprimer les types déjà utilisés par un compte.
                // Tous les exemplaires non utilisés de ce même type sont des doublons superflus.
                toDelete.AddRange(unusedInGroup);
            }
            else
            {
                // Aucun compte n'utilise ce type : on conserve un exemplaire unique (le plus ancien)
                // et on supprime tous les doublons excédentaires non utilisés.
                var canonical = unusedInGroup
                    .OrderBy(t => t.CreatedAtUtc)
                    .ThenBy(t => t.Id)
                    .First();

                toDelete.AddRange(unusedInGroup.Where(t => t.Id != canonical.Id));
            }
        }

        if (toDelete.Count == 0)
        {
            return 0;
        }

        // 4. Suppression des traductions orphelines associées aux doublons supprimés
        var deletedIds = toDelete.Select(t => t.Id).ToHashSet();
        var orphanTranslations = await dbContext.EntityTranslations
            .Where(t => t.EntityType == "CustomAccountType" && deletedIds.Contains(t.EntityId))
            .ToListAsync(cancellationToken);

        if (orphanTranslations.Count > 0)
        {
            dbContext.EntityTranslations.RemoveRange(orphanTranslations);
        }

        dbContext.CustomAccountTypes.RemoveRange(toDelete);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
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
