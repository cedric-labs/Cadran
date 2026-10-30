using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Cumule les soldes des comptes en liquidités, épargne et dettes pour calculer le patrimoine net.
/// </summary>
internal sealed class NetWorthTotals
{
    /// <summary>Total des liquidités.</summary>
    public decimal Liquid { get; private set; }

    /// <summary>Total de l'épargne et des placements.</summary>
    public decimal Savings { get; private set; }

    /// <summary>Total des dettes, exprimé en valeur positive.</summary>
    public decimal Debts { get; private set; }

    /// <summary>Liquidités plus épargne moins dettes.</summary>
    public decimal NetWorth => Liquid + Savings - Debts;

    /// <summary>
    /// Ajoute le solde d'un compte à la rubrique correspondant à son groupe : un solde négatif de liquidités ou de dette devient une dette.
    /// </summary>
    public void Add(BankAccount account, decimal balance)
    {
        switch (ResolveGroup(account))
        {
            case AccountCategoryGroup.Liquid when balance < 0:
            case AccountCategoryGroup.Debt when balance < 0:
                Debts += Math.Abs(balance);
                break;
            case AccountCategoryGroup.Liquid:
            case AccountCategoryGroup.Debt:
                Liquid += balance;
                break;
            default:
                Savings += balance;
                break;
        }
    }

    // Le type configurable en base prime ; à défaut, le type énuméré historique décide.
    private static AccountCategoryGroup ResolveGroup(BankAccount account)
    {
        if (account.CustomAccountType != null)
        {
            return account.CustomAccountType.CategoryGroup;
        }

        return account.Type.IsLiquid() || account.Type == AccountType.CreditCard
            ? AccountCategoryGroup.Liquid
            : AccountCategoryGroup.Savings;
    }
}
