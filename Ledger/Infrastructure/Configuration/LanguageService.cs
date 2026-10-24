namespace Ledger.Infrastructure.Configuration;

/// <summary>
/// Informations décrivant une langue supportée par l'application.
/// </summary>
/// <param name="Code">Code ISO de la langue.</param>
/// <param name="DisplayName">Nom d'affichage avec drapeau.</param>
/// <param name="FlagEmoji">Emoji du drapeau associé.</param>
public record LanguageInfo(string Code, string DisplayName, string FlagEmoji);

/// <summary>
/// Contrat de service pour la gestion et la découverte des langues supportées par l'application.
/// </summary>
public interface ILanguageService
{
    /// <summary>
    /// Retourne la liste ordonnée des langues disponibles dans le système.
    /// </summary>
    /// <returns>Collection en lecture seule des informations de langue.</returns>
    IReadOnlyList<LanguageInfo> GetSupportedLanguages();

    /// <summary>
    /// Vérifie si un code de culture donné est pris en charge.
    /// </summary>
    /// <param name="code">Code ISO de la langue.</param>
    /// <returns><c>true</c> si la langue est supportée, sinon <c>false</c>.</returns>
    bool IsSupported(string? code);
}

/// <summary>
/// Fournisseur des langues disponibles basé sur les fichiers de localisation du disque.
/// Délègue son implémentation au framework partagé <see cref="LocaleForge.Localization.LanguageService"/>.
/// </summary>
/// <param name="localesPath">Chemin du répertoire contenant les fichiers JSON de localisation.</param>
public sealed class LanguageService(string localesPath) : ILanguageService
{
    private readonly LocaleForge.Localization.LanguageService _inner = new(localesPath);
    private readonly Lazy<IReadOnlyList<LanguageInfo>> _mappedLanguages = new(() =>
        new LocaleForge.Localization.LanguageService(localesPath)
            .GetSupportedLanguages()
            .Select(l => new LanguageInfo(l.Code, l.DisplayName, l.FlagEmoji))
            .ToList());

    /// <inheritdoc />
    public IReadOnlyList<LanguageInfo> GetSupportedLanguages() => _mappedLanguages.Value;

    /// <inheritdoc />
    public bool IsSupported(string? code) => _inner.IsSupported(code);
}
