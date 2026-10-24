using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Ledger.Domain.ValueObjects;

/// <summary>
/// Value Object immuable représentant une couleur au format hexadécimal CSS valide (ex: #3b82f6, #fff).
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ColorHex : IComparable<ColorHex>
{
    /// <summary>
    /// Couleur bleue par défaut de l'application (#3b82f6).
    /// </summary>
    public static readonly ColorHex Default = new("#3b82f6");

    /// <summary>
    /// Couleur grise neutre (#64748b).
    /// </summary>
    public static readonly ColorHex Slate = new("#64748b");

    /// <summary>
    /// Valeur hexadécimale normalisée en minuscules avec le préfixe '#'.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="ColorHex"/> après validation du motif hexadécimal.
    /// </summary>
    /// <param name="hex">Chaîne au format #RGB, #RGBA, #RRGGBB ou #RRGGBBAA.</param>
    /// <exception cref="ArgumentException">Lancée si la chaîne n'est pas un code couleur hexadécimal CSS valide.</exception>
    public ColorHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);

        var trimmed = hex.Trim();
        if (!IsValidHexFormat(trimmed))
        {
            throw new ArgumentException("Le code couleur doit débuter par '#' et être suivi de 3, 4, 6 ou 8 chiffres hexadécimaux.", nameof(hex));
        }

        Value = trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// Tente d'analyser une chaîne de caractères en couleur hexadécimale CSS.
    /// </summary>
    /// <param name="input">Chaîne candidate.</param>
    /// <param name="color">Couleur résultante si l'analyse réussit.</param>
    /// <returns><c>true</c> si l'analyse a réussi ; sinon, <c>false</c>.</returns>
    public static bool TryParse(string? input, [NotNullWhen(true)] out ColorHex? color)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            color = null;
            return false;
        }

        var trimmed = input.Trim();
        if (!IsValidHexFormat(trimmed))
        {
            color = null;
            return false;
        }

        color = new ColorHex(trimmed);
        return true;
    }

    /// <summary>
    /// Crée une couleur hexadécimale ou retourne la couleur par défaut (#3b82f6) si l'entrée est vide ou invalide.
    /// </summary>
    /// <param name="hex">Code hexadécimal optionnel.</param>
    /// <returns>Couleur hexadécimale validée ou couleur par défaut.</returns>
    public static ColorHex FromOrDefault(string? hex)
    {
        return TryParse(hex, out var result) ? result.Value : Default;
    }

    private static bool IsValidHexFormat(string text)
    {
        if (text.Length is not (4 or 5 or 7 or 9) || text[0] != '#')
        {
            return false;
        }

        for (int i = 1; i < text.Length; i++)
        {
            if (!char.IsAsciiHexDigit(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value ?? "#3b82f6";

    /// <summary>
    /// Conversion implicite vers une chaîne de caractères.
    /// </summary>
    public static implicit operator string(ColorHex color) => color.ToString();

    /// <inheritdoc />
    public int CompareTo(ColorHex other) => string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Opérateur inférieur.
    /// </summary>
    public static bool operator <(ColorHex left, ColorHex right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Opérateur inférieur ou égal.
    /// </summary>
    public static bool operator <=(ColorHex left, ColorHex right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Opérateur supérieur.
    /// </summary>
    public static bool operator >(ColorHex left, ColorHex right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Opérateur supérieur ou égal.
    /// </summary>
    public static bool operator >=(ColorHex left, ColorHex right) => left.CompareTo(right) >= 0;
}
