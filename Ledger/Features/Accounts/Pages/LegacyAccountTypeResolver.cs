using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;

namespace Ledger.Features.Accounts.Pages;

/// <summary>
/// Déduit le type énuméré historique d'un compte à partir du code du type personnalisé choisi.
/// </summary>
internal static class LegacyAccountTypeResolver
{
    /// <summary>
    /// Retourne le type énuméré correspondant au code du type personnalisé, ou <paramref name="fallback"/> si aucun type n'est choisi ou si son code est inconnu.
    /// </summary>
    public static async Task<AccountType> ResolveAsync(IAccountTypeService accountTypeService, Guid? customTypeId, AccountType fallback, CancellationToken cancellationToken)
    {
        if (!customTypeId.HasValue)
        {
            return fallback;
        }

        var chosenType = await accountTypeService.GetByIdAsync(customTypeId.Value, cancellationToken).ConfigureAwait(false);
        return chosenType != null && !string.IsNullOrEmpty(chosenType.Code) && Enum.TryParse<AccountType>(chosenType.Code, out var parsedType)
            ? parsedType
            : fallback;
    }
}
