using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Détermine les traductions anglaise, espagnole et allemande manquantes pour les statuts d'opérations par défaut.
/// </summary>
internal static class StatusTranslationPlanner
{
    private static readonly IReadOnlyDictionary<string, (string En, string Es, string De)> DefaultStatusTranslations =
        new Dictionary<string, (string En, string Es, string De)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Reconciled"] = ("Reconciled", "Conciliada", "Abgeglichen"),
            ["Cleared"] = ("Cleared", "Contabilizada", "Gebucht"),
            ["Pending"] = ("Pending", "Pendiente", "Ausstehend")
        };

    /// <summary>
    /// Retourne les traductions du champ « Name » absentes de <paramref name="existingKeys"/> (clés « idStatut:langue »).
    /// Les statuts sans code connu sont ignorés.
    /// </summary>
    public static List<EntityTranslation> PlanMissing(IEnumerable<TransactionCustomStatus> statuses, ISet<string> existingKeys)
    {
        var missing = new List<EntityTranslation>();
        foreach (var status in statuses)
        {
            if (string.IsNullOrEmpty(status.Code) || !DefaultStatusTranslations.TryGetValue(status.Code, out var translation))
            {
                continue;
            }

            AddWhenMissing(missing, existingKeys, status.Id, "en", translation.En);
            AddWhenMissing(missing, existingKeys, status.Id, "es", translation.Es);
            AddWhenMissing(missing, existingKeys, status.Id, "de", translation.De);
        }

        return missing;
    }

    private static void AddWhenMissing(List<EntityTranslation> missing, ISet<string> existingKeys, Guid statusId, string languageCode, string value)
    {
        if (existingKeys.Contains($"{statusId}:{languageCode}") || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        missing.Add(new EntityTranslation
        {
            EntityType = "TransactionCustomStatus",
            EntityId = statusId,
            Field = "Name",
            LanguageCode = languageCode,
            Translation = value
        });
    }
}
