using System.Globalization;
using Ledger.Domain.Entities;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Infrastructure.Configuration;

/// <summary>
/// Contrat de service pour la gestion des traductions dynamiques d'entités stockées en base.
/// </summary>
public interface ILocalizedEntityService
{
    /// <summary>
    /// Récupère la traduction d'un champ pour une entité donnée.
    /// </summary>
    /// <param name="entityType">Nom de l'entité (ex: Category, AccountType).</param>
    /// <param name="entityId">Identifiant unique de l'entité.</param>
    /// <param name="field">Nom du champ traduit.</param>
    /// <param name="fallbackValue">Valeur de repli si aucune traduction n'est trouvée.</param>
    /// <param name="languageCode">Code ISO de la langue (par défaut la langue courante de l'interface).</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Texte traduit ou valeur de repli.</returns>
    Task<string> GetTranslationAsync(string entityType, Guid entityId, string field, string fallbackValue, string? languageCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère les traductions d'un champ pour un ensemble d'entités.
    /// </summary>
    /// <param name="entityType">Nom de l'entité.</param>
    /// <param name="entityIds">Liste d'identifiants uniques.</param>
    /// <param name="field">Nom du champ traduit.</param>
    /// <param name="languageCode">Code ISO de la langue.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Dictionnaire associant chaque identifiant à sa traduction.</returns>
    Task<Dictionary<Guid, string>> GetTranslationsAsync(string entityType, IEnumerable<Guid> entityIds, string field, string? languageCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enregistre ou met à jour la traduction d'un champ d'une entité.
    /// </summary>
    /// <param name="entityType">Nom de l'entité.</param>
    /// <param name="entityId">Identifiant unique de l'entité.</param>
    /// <param name="field">Nom du champ.</param>
    /// <param name="languageCode">Code ISO de la langue.</param>
    /// <param name="translation">Texte traduit (si vide, la traduction existante est supprimée).</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    Task SetTranslationAsync(string entityType, Guid entityId, string field, string languageCode, string translation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère toutes les traductions existantes pour un champ spécifique d'une entité.
    /// </summary>
    /// <param name="entityType">Nom de l'entité.</param>
    /// <param name="entityId">Identifiant unique de l'entité.</param>
    /// <param name="field">Nom du champ.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Dictionnaire associant le code langue à sa traduction.</returns>
    Task<Dictionary<string, string>> GetAllTranslationsForEntityAsync(string entityType, Guid entityId, string field, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service gérant les traductions des entités de la base de données.
/// </summary>
public sealed class LocalizedEntityService(AppDbContext dbContext) : ILocalizedEntityService
{
    public async Task<string> GetTranslationAsync(string entityType, Guid entityId, string field, string fallbackValue, string? languageCode = null, CancellationToken cancellationToken = default)
    {
        var lang = NormalizeLanguageCode(languageCode);
        var translations = await dbContext.EntityTranslations
            .Where(t => t.EntityType == entityType && t.Field == field && t.LanguageCode == lang)
            .ToListAsync(cancellationToken);

        var match = translations.FirstOrDefault(t => t.EntityId == entityId);
        return match != null && !string.IsNullOrWhiteSpace(match.Translation) ? match.Translation : fallbackValue;
    }

    public async Task<Dictionary<Guid, string>> GetTranslationsAsync(string entityType, IEnumerable<Guid> entityIds, string field, string? languageCode = null, CancellationToken cancellationToken = default)
    {
        var lang = NormalizeLanguageCode(languageCode);
        var idSet = entityIds.ToHashSet();

        var translations = await dbContext.EntityTranslations
            .Where(t => t.EntityType == entityType && t.Field == field && t.LanguageCode == lang)
            .ToListAsync(cancellationToken);

        return translations
            .Where(t => idSet.Contains(t.EntityId))
            .ToDictionary(t => t.EntityId, t => t.Translation);
    }

    public async Task SetTranslationAsync(string entityType, Guid entityId, string field, string languageCode, string translation, CancellationToken cancellationToken = default)
    {
        var lang = NormalizeLanguageCode(languageCode);
        var existingList = await dbContext.EntityTranslations
            .Where(t => t.EntityType == entityType && t.Field == field && t.LanguageCode == lang)
            .ToListAsync(cancellationToken);
        var existing = existingList.FirstOrDefault(t => t.EntityId == entityId);

        if (string.IsNullOrWhiteSpace(translation))
        {
            if (existing != null)
            {
                dbContext.EntityTranslations.Remove(existing);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        if (existing == null)
        {
            dbContext.EntityTranslations.Add(new EntityTranslation
            {
                EntityType = entityType,
                EntityId = entityId,
                Field = field,
                LanguageCode = lang,
                Translation = translation.Trim()
            });
        }
        else
        {
            existing.Translation = translation.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetAllTranslationsForEntityAsync(string entityType, Guid entityId, string field, CancellationToken cancellationToken = default)
    {
        var translations = await dbContext.EntityTranslations
            .Where(t => t.EntityType == entityType && t.Field == field)
            .ToListAsync(cancellationToken);

        return translations
            .Where(t => t.EntityId == entityId)
            .ToDictionary(t => t.LanguageCode, t => t.Translation);
    }

    private static string NormalizeLanguageCode(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }
        return lang.ToLowerInvariant();
    }
}
