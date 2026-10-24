namespace Ledger.Features.Categories.Services;

/// <summary>
/// Service responsable de l'initialisation, de la synchronisation et de la restauration des catégories par défaut
/// et de leurs traductions multilingues issues du catalogue JSON (Resources/default-categories.json).
/// </summary>
public interface ICategorySeedService
{
    /// <summary>
    /// Initialise la hiérarchie complète des catégories par défaut et leurs traductions
    /// si l'utilisateur ne possède encore aucune catégorie enregistrée.
    /// Complète également les traductions manquantes pour les catégories déjà existantes.
    /// </summary>
    /// <param name="userId">Identifiant de l'utilisateur cible.</param>
    /// <param name="cancellationToken">Jeton d'annulation facultatif.</param>
    Task EnsureDefaultCategoriesForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restaure ou recharge les catégories par défaut manquantes sans créer de doublons
    /// pour les catégories déjà existantes en base de données.
    /// </summary>
    /// <param name="userId">Identifiant de l'utilisateur cible.</param>
    /// <param name="cancellationToken">Jeton d'annulation facultatif.</param>
    /// <returns>Le nombre de catégories nouvellement créées.</returns>
    Task<int> RestoreDefaultCategoriesAsync(string userId, CancellationToken cancellationToken = default);
}
