using System.Globalization;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Interprète un montant saisi à la main, avec point ou virgule décimale et espaces de regroupement.
/// </summary>
public static class ManualAmountParser
{
    /// <summary>
    /// Convertit le texte saisi ; renvoie 0 lorsqu'il est vide ou illisible, ce que la validation de création rejette comme montant invalide.
    /// </summary>
    public static decimal ParseOrZero(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        var normalized = string.Concat(text.Where(c => !char.IsWhiteSpace(c))).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : 0m;
    }
}
