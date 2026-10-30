using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Ledger.Tests.E2E.Infrastructure;

/// <summary>
/// Calcule le code TOTP à 6 chiffres (RFC 6238, SHA-1, pas de 30 s) attendu par l'authentificateur d'ASP.NET Core Identity.
/// </summary>
public static class TotpCodeGenerator
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int StepSeconds = 30;

    /// <summary>
    /// Calcule le code valide à l'instant donné pour la clé partagée affichée par la page d'activation MFA.
    /// </summary>
    /// <param name="sharedKey">Clé Base32, avec ou sans espaces de regroupement.</param>
    /// <param name="at">Instant pour lequel calculer le code.</param>
    /// <returns>Le code sur six chiffres.</returns>
    public static string Generate(string sharedKey, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKey);

        var key = DecodeBase32(sharedKey);
        var counter = at.ToUnixTimeSeconds() / StepSeconds;
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(key, counterBytes, hash);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];

        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string value)
    {
        var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(normalized.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var character in normalized)
        {
            var index = Base32Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new FormatException($"Caractère Base32 invalide dans la clé partagée : '{character}'.");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return [.. bytes];
    }
}
