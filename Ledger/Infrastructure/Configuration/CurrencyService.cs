using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Ledger.Infrastructure.Configuration;

/// <summary>
/// Informations décrivant une devise monétaire prise en charge par l'application.
/// </summary>
/// <param name="Code">Code ISO-4217 à 3 lettres.</param>
/// <param name="Symbol">Symbole monétaire.</param>
/// <param name="DisplayName">Libellé complet d'affichage.</param>
/// <param name="FlagEmoji">Emoji du drapeau associé.</param>
public record CurrencyInfo(string Code, string Symbol, string DisplayName, string FlagEmoji = "");

/// <summary>
/// Service de gestion et de résolution dynamique des devises configurables.
/// </summary>
public interface ICurrencyService
{
    /// <summary>
    /// Retourne la liste des devises supportées par l'application.
    /// </summary>
    /// <returns>Collection en lecture seule des devises configurées.</returns>
    IReadOnlyList<CurrencyInfo> GetSupportedCurrencies();

    /// <summary>
    /// Résout les informations complètes d'une devise à partir de son code ISO.
    /// </summary>
    /// <param name="code">Code ISO à 3 lettres (ex: EUR, USD).</param>
    /// <returns>Informations de devise correspondante ou devise par défaut.</returns>
    CurrencyInfo GetCurrency(string? code);

    /// <summary>
    /// Récupère le symbole monétaire associé à un code devise.
    /// </summary>
    /// <param name="code">Code ISO de la devise.</param>
    /// <returns>Symbole de devise (ex: €, $).</returns>
    string GetSymbol(string? code);

    /// <summary>
    /// Formate un montant décimal avec le symbole et les séparateurs de la devise et culture cibles.
    /// </summary>
    /// <param name="amount">Montant à formater.</param>
    /// <param name="currencyCode">Code de la devise cible.</param>
    /// <param name="includeCents">Indique s'il faut afficher les centimes.</param>
    /// <param name="culture">Culture régionale optionnelle pour le formatage.</param>
    /// <returns>Montant mis en forme sous forme textuelle.</returns>
    string FormatAmount(decimal amount, string? currencyCode = null, bool includeCents = true, CultureInfo? culture = null);
}

/// <summary>
/// Implémentation du service de devises s'appuyant sur les paramètres SQLite (AppSettings)
/// et déléguant la résolution et le formatage au framework partagé <see cref="LocaleForge.Currencies.CurrencyService"/>.
/// </summary>
/// <param name="configuration">Configuration applicative.</param>
public sealed class CurrencyService(IConfiguration configuration) : ICurrencyService
{
    private readonly LocaleForge.Currencies.CurrencyService _inner = new(configuration);
    private readonly Lazy<IReadOnlyList<CurrencyInfo>> _mappedCurrencies = new(() =>
        new LocaleForge.Currencies.CurrencyService(configuration)
            .GetSupportedCurrencies()
            .Select(c => new CurrencyInfo(c.Code, c.Symbol, c.DisplayName, c.FlagEmoji))
            .ToList());

    /// <summary>
    /// Liste de référence des devises internationales par défaut.
    /// </summary>
    public static readonly IReadOnlyList<CurrencyInfo> DefaultCurrencies =
        LocaleForge.Currencies.CurrencyService.DefaultCurrencies
            .Select(c => new CurrencyInfo(c.Code, c.Symbol, c.DisplayName, c.FlagEmoji))
            .ToList();

    /// <inheritdoc />
    public IReadOnlyList<CurrencyInfo> GetSupportedCurrencies() => _mappedCurrencies.Value;

    /// <inheritdoc />
    public CurrencyInfo GetCurrency(string? code)
    {
        var c = _inner.GetCurrency(code);
        return new CurrencyInfo(c.Code, c.Symbol, c.DisplayName, c.FlagEmoji);
    }

    /// <inheritdoc />
    public string GetSymbol(string? code) => _inner.GetSymbol(code);

    /// <inheritdoc />
    public string FormatAmount(decimal amount, string? currencyCode = null, bool includeCents = true, CultureInfo? culture = null) =>
        _inner.FormatAmount(amount, currencyCode, includeCents, culture);

    /// <summary>
    /// Résout de façon statique le symbole monétaire associé à un code ISO.
    /// </summary>
    /// <param name="code">Code ISO à 3 lettres.</param>
    /// <returns>Symbole monétaire.</returns>
    public static string GetStaticSymbol(string? code) =>
        LocaleForge.Currencies.CurrencyService.GetStaticSymbol(code);
}

/// <summary>
/// Méthodes d'extension permettant de formater élégamment tout montant monétaire
/// selon la devise d'un compte bancaire dans les vues Razor et services.
/// </summary>
public static class CurrencyExtensions
{
    private static ICurrencyService? _service;

    /// <summary>
    /// Configure l'instance active du service de devises.
    /// </summary>
    /// <param name="service">Service de devises actif.</param>
    public static void Configure(ICurrencyService? service)
    {
        _service = service;
    }

    /// <summary>
    /// Formate un montant décimal dans la devise spécifiée.
    /// </summary>
    public static string ToCurrency(this decimal amount, string? currencyCode = null, bool includeCents = true, CultureInfo? culture = null)
    {
        if (_service is not null)
        {
            return _service.FormatAmount(amount, currencyCode, includeCents, culture);
        }

        var symbol = CurrencyService.GetStaticSymbol(currencyCode);
        return LocaleForge.Currencies.CurrencyService.FormatWithSymbol(amount, symbol, includeCents, culture);
    }

    /// <summary>
    /// Formate un montant décimal nullable dans la devise spécifiée.
    /// </summary>
    public static string ToCurrency(this decimal? amount, string? currencyCode = null, bool includeCents = true, CultureInfo? culture = null)
    {
        if (!amount.HasValue)
        {
            return string.Empty;
        }

        return amount.Value.ToCurrency(currencyCode, includeCents, culture);
    }
}
