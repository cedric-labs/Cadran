using Microsoft.Extensions.Localization;

namespace Ledger.Infrastructure.Configuration;

/// <summary>
/// Implémentation de <see cref="IStringLocalizer"/> basée sur des fichiers JSON situés dans <c>Resources/Locales/{culture}.json</c>.
/// Délègue son implémentation au framework partagé <see cref="LocaleForge.Localization.JsonStringLocalizer"/>.
/// </summary>
/// <param name="resourcesPath">Chemin du répertoire contenant les fichiers JSON de localisation.</param>
public sealed class JsonStringLocalizer(string resourcesPath) : IStringLocalizer
{
    private readonly LocaleForge.Localization.JsonStringLocalizer _inner = new(resourcesPath);

    /// <inheritdoc />
    public LocalizedString this[string name] => _inner[name];

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments] => _inner[name, arguments];

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        _inner.GetAllStrings(includeParentCultures);
}

/// <summary>
/// Fabrique <see cref="IStringLocalizerFactory"/> instanciant et mettant en cache un <see cref="JsonStringLocalizer"/>.
/// Délègue son implémentation au framework partagé <see cref="LocaleForge.Localization.JsonStringLocalizerFactory"/>.
/// </summary>
/// <param name="resourcesPath">Chemin du répertoire contenant les fichiers JSON de localisation.</param>
public sealed class JsonStringLocalizerFactory(string resourcesPath) : IStringLocalizerFactory
{
    private readonly LocaleForge.Localization.JsonStringLocalizerFactory _inner = new(resourcesPath);

    /// <inheritdoc />
    public IStringLocalizer Create(Type resourceSource) => _inner.Create(resourceSource);

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName, string location) => _inner.Create(baseName, location);
}
