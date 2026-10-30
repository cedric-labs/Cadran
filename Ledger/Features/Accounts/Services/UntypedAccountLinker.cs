using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Rattache les comptes bancaires dépourvus de type personnalisé au type de leur propriétaire correspondant à leur type énuméré.
/// </summary>
internal static class UntypedAccountLinker
{
    /// <summary>
    /// Associe chaque compte au type de même code (ou, à défaut, au type « Checking ») du même utilisateur
    /// et ajoute le type retenu à <paramref name="usedTypeIds"/>.
    /// </summary>
    public static void LinkToMatchingTypes(IEnumerable<BankAccount> accounts, IReadOnlyCollection<CustomAccountType> allTypes, ISet<Guid> usedTypeIds)
    {
        foreach (var acc in accounts)
        {
            var code = acc.Type.ToString();
            var matchingType = allTypes.FirstOrDefault(t => string.Equals(t.UserId, acc.UserId, StringComparison.Ordinal) && string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase))
                ?? allTypes.FirstOrDefault(t => string.Equals(t.UserId, acc.UserId, StringComparison.Ordinal) && string.Equals(t.Code, "Checking", StringComparison.OrdinalIgnoreCase));

            if (matchingType != null)
            {
                acc.CustomAccountTypeId = matchingType.Id;
                usedTypeIds.Add(matchingType.Id);
            }
        }
    }
}
