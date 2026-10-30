using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Choisit les types de comptes superflus d'un même utilisateur : mêmes code (ou, à défaut, même nom) sans compte rattaché.
/// </summary>
internal static class DuplicateAccountTypeSelector
{
    /// <summary>
    /// Retourne les doublons à supprimer. Un type utilisé n'est jamais retenu ; si aucun exemplaire du groupe n'est utilisé,
    /// le plus ancien est conservé.
    /// </summary>
    public static List<CustomAccountType> SelectRemovable(IEnumerable<CustomAccountType> allTypes, ISet<Guid> usedTypeIds)
    {
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

        return toDelete;
    }
}
