using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Ledger.Features.Accounts.Entities;

/// <summary>
/// Types de comptes et supports financiers selon la typologie bancaire française.
/// </summary>
public enum AccountType
{
    // Comptes de gestion courante / liquidités
    [Display(Name = "Compte de Dépôt")]
    Checking = 0,

    [Display(Name = "Carte à débit différé / Crédit")]
    CreditCard = 2,

    [Display(Name = "Portefeuille / Espèces")]
    Cash = 4,

    // Épargne réglementée
    [Display(Name = "Livret A")]
    LivretA = 10,

    [Display(Name = "LDD Solidaire")]
    Ldds = 11,

    [Display(Name = "Livret d'Épargne Populaire (LEP)")]
    Lep = 12,

    [Display(Name = "Livret Jeune")]
    LivretJeune = 13,

    // Épargne bancaire et logement
    [Display(Name = "Livret bancaire (CSL)")]
    Savings = 1,

    [Display(Name = "Carré mauve (PEL)")]
    Pel = 20,

    [Display(Name = "Compte Épargne Logement (CEL)")]
    Cel = 21,

    [Display(Name = "Compte à terme (CAT)")]
    Cat = 22,

    // Placements, bourse et titres
    [Display(Name = "Plan d'Épargne en Actions (PEA)")]
    Pea = 30,

    [Display(Name = "Compte-Titres Ordinaire (CTO)")]
    Investment = 3,

    [Display(Name = "Compte Parts Sociales")]
    PartsSociales = 31,

    [Display(Name = "Assurance-Vie")]
    AssuranceVie = 32,

    [Display(Name = "Plan d'Épargne Retraite (PER)")]
    Per = 33,

    [Display(Name = "Crypto-monnaies / Autres actifs")]
    Crypto = 34
}

public static class AccountTypeExtensions
{
    /// <summary>
    /// Retourne le nom convivial français configuré dans l'attribut Display.
    /// </summary>
    public static string GetDisplayName(this AccountType type)
    {
        var member = typeof(AccountType).GetMember(type.ToString());
        if (member.Length > 0)
        {
            var displayAttr = member[0].GetCustomAttribute<DisplayAttribute>();
            if (displayAttr?.Name != null)
            {
                return displayAttr.Name;
            }
        }
        return type.ToString();
    }

    /// <summary>
    /// Indique si ce type de compte correspond à des liquidités courantes (compte de dépôt, espèces).
    /// </summary>
    public static bool IsLiquid(this AccountType type)
    {
        return type is AccountType.Checking or AccountType.Cash;
    }

    /// <summary>
    /// Indique si ce type de compte correspond à de l'épargne ou des investissements.
    /// </summary>
    public static bool IsSavingsOrInvestment(this AccountType type)
    {
        return type is AccountType.LivretA
            or AccountType.Ldds
            or AccountType.Lep
            or AccountType.LivretJeune
            or AccountType.Savings
            or AccountType.Pel
            or AccountType.Cel
            or AccountType.Cat
            or AccountType.Pea
            or AccountType.Investment
            or AccountType.PartsSociales
            or AccountType.AssuranceVie
            or AccountType.Per
            or AccountType.Crypto;
    }

    /// <summary>
    /// Retourne la classe CSS Bootstrap pour le badge du type de compte.
    /// </summary>
    public static string GetBadgeClass(this AccountType type)
    {
        return type switch
        {
            AccountType.Checking => "bg-primary-subtle text-primary",
            AccountType.CreditCard => "bg-warning-subtle text-warning",
            AccountType.Cash => "bg-secondary-subtle text-secondary",
            AccountType.LivretA or AccountType.Ldds or AccountType.Lep or AccountType.LivretJeune or AccountType.Savings or AccountType.Pel or AccountType.Cel or AccountType.Cat => "bg-info-subtle text-info",
            AccountType.Pea or AccountType.Investment or AccountType.PartsSociales or AccountType.AssuranceVie or AccountType.Per or AccountType.Crypto => "bg-success-subtle text-success",
            _ => "bg-secondary-subtle text-secondary"
        };
    }
}
