using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Types de comptes français créés par défaut pour chaque utilisateur : liquidités, épargne réglementée, épargne logement, titres.
/// </summary>
internal static class DefaultAccountTypeCatalog
{
    /// <summary>
    /// Crée les 17 types système de l'utilisateur, numérotés de 1 à 17 dans l'ordre d'affichage.
    /// </summary>
    public static List<CustomAccountType> CreateFor(string userId)
    {
        return new List<CustomAccountType>
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
    }
}
