using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Ledger.Domain.ValueObjects;

/// <summary>
/// Value Object immuable représentant un code de devise conforme à la norme ISO-4217 (ex: EUR, USD, GBP).
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct CurrencyCode : IComparable<CurrencyCode>
{
    /// <summary>
    /// Devise par défaut de l'application (Euro - EUR).
    /// </summary>
    public static readonly CurrencyCode Eur = new("EUR");

    /// <summary>
    /// Dollar américain (USD).
    /// </summary>
    public static readonly CurrencyCode Usd = new("USD");

    /// <summary>
    /// Livre sterling (GBP).
    /// </summary>
    public static readonly CurrencyCode Gbp = new("GBP");

    /// <summary>
    /// Franc suisse (CHF).
    /// </summary>
    public static readonly CurrencyCode Chf = new("CHF");

    /// <summary>
    /// Valeur normalisée du code devise (3 lettres majuscules).
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="CurrencyCode"/> après validation stricte.
    /// </summary>
    /// <param name="code">Code ISO à 3 lettres alphabétiques.</param>
    /// <exception cref="ArgumentException">Lancée si le code n'est pas composé de 3 lettres alphabétiques.</exception>
    public CurrencyCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var trimmed = code.Trim();
        if (trimmed.Length != 3 || !trimmed.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("Un code devise ISO-4217 doit être composé d'exactement 3 lettres alphabétiques.", nameof(code));
        }

        Value = trimmed.ToUpperInvariant();
    }

    /// <summary>
    /// Tente d'analyser une chaîne de caractères en code de devise ISO-4217.
    /// </summary>
    /// <param name="input">Chaîne candidate.</param>
    /// <param name="currencyCode">Code de devise résultant si l'analyse réussit.</param>
    /// <returns><c>true</c> si l'analyse a réussi ; sinon, <c>false</c>.</returns>
    public static bool TryParse(string? input, [NotNullWhen(true)] out CurrencyCode? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            currencyCode = null;
            return false;
        }

        var trimmed = input.Trim();
        if (trimmed.Length != 3 || !trimmed.All(char.IsAsciiLetter))
        {
            currencyCode = null;
            return false;
        }

        currencyCode = new CurrencyCode(trimmed);
        return true;
    }

    /// <summary>
    /// Crée un code de devise ou retourne le code par défaut (EUR) si l'entrée est vide ou invalide.
    /// </summary>
    /// <param name="code">Code ISO optionnel.</param>
    /// <returns>Code devise validé ou EUR par défaut.</returns>
    public static CurrencyCode FromOrDefault(string? code)
    {
        return TryParse(code, out var result) ? result.Value : Eur;
    }

    /// <inheritdoc />
    public override string ToString() => Value ?? "EUR";

    /// <summary>
    /// Conversion implicite vers une chaîne de caractères.
    /// </summary>
    public static implicit operator string(CurrencyCode code) => code.ToString();

    /// <inheritdoc />
    public int CompareTo(CurrencyCode other) => string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Opérateur inférieur.
    /// </summary>
    public static bool operator <(CurrencyCode left, CurrencyCode right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Opérateur inférieur ou égal.
    /// </summary>
    public static bool operator <=(CurrencyCode left, CurrencyCode right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Opérateur supérieur.
    /// </summary>
    public static bool operator >(CurrencyCode left, CurrencyCode right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Opérateur supérieur ou égal.
    /// </summary>
    public static bool operator >=(CurrencyCode left, CurrencyCode right) => left.CompareTo(right) >= 0;
}
