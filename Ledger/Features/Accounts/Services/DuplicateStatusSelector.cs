using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Choisit les statuts d'opérations superflus d'un même utilisateur : mêmes code (ou, à défaut, même nom) sans opération rattachée.
/// </summary>
internal static class DuplicateStatusSelector
{
    /// <summary>
    /// Retourne les doublons à supprimer. Un statut utilisé n'est jamais retenu ; si aucun exemplaire du groupe n'est utilisé,
    /// le plus ancien est conservé.
    /// </summary>
    public static List<TransactionCustomStatus> SelectRemovable(IEnumerable<TransactionCustomStatus> allStatuses, ISet<Guid> usedStatusIds)
    {
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

        return toDelete;
    }
}
