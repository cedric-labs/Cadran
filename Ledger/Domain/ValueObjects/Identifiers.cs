using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Ledger.Domain.ValueObjects;

/// <summary>
/// Identifiant typé et validé d'un compte bancaire.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct BankAccountId : IComparable<BankAccountId>
{
    /// <summary>
    /// Valeur sous-jacente du GUID.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="BankAccountId"/> après validation de non-vacuité.
    /// </summary>
    /// <param name="value">GUID non vide.</param>
    /// <exception cref="ArgumentException">Lancée si le GUID est vide.</exception>
    public BankAccountId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de compte bancaire ne peut pas être un GUID vide.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Génère un nouvel identifiant unique de compte bancaire.
    /// </summary>
    public static BankAccountId New() => new(Guid.NewGuid());

    /// <summary>
    /// Tente d'analyser une chaîne GUID en <see cref="BankAccountId"/>.
    /// </summary>
    public static bool TryParse(string? input, [NotNullWhen(true)] out BankAccountId? id)
    {
        if (Guid.TryParse(input, out var guid) && guid != Guid.Empty)
        {
            id = new BankAccountId(guid);
            return true;
        }

        id = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Conversion implicite vers <see cref="Guid"/>.
    /// </summary>
    public static implicit operator Guid(BankAccountId id) => id.Value;

    /// <inheritdoc />
    public int CompareTo(BankAccountId other) => Value.CompareTo(other.Value);

    /// <summary>
    /// Opérateur inférieur.
    /// </summary>
    public static bool operator <(BankAccountId left, BankAccountId right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Opérateur inférieur ou égal.
    /// </summary>
    public static bool operator <=(BankAccountId left, BankAccountId right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Opérateur supérieur.
    /// </summary>
    public static bool operator >(BankAccountId left, BankAccountId right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Opérateur supérieur ou égal.
    /// </summary>
    public static bool operator >=(BankAccountId left, BankAccountId right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Identifiant typé et validé d'une opération financière (transaction).
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TransactionId : IComparable<TransactionId>
{
    /// <summary>
    /// Valeur sous-jacente du GUID.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="TransactionId"/> après validation de non-vacuité.
    /// </summary>
    /// <param name="value">GUID non vide.</param>
    /// <exception cref="ArgumentException">Lancée si le GUID est vide.</exception>
    public TransactionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant d'opération ne peut pas être un GUID vide.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Génère un nouvel identifiant unique d'opération.
    /// </summary>
    public static TransactionId New() => new(Guid.NewGuid());

    /// <summary>
    /// Tente d'analyser une chaîne GUID en <see cref="TransactionId"/>.
    /// </summary>
    public static bool TryParse(string? input, [NotNullWhen(true)] out TransactionId? id)
    {
        if (Guid.TryParse(input, out var guid) && guid != Guid.Empty)
        {
            id = new TransactionId(guid);
            return true;
        }

        id = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Conversion implicite vers <see cref="Guid"/>.
    /// </summary>
    public static implicit operator Guid(TransactionId id) => id.Value;

    /// <inheritdoc />
    public int CompareTo(TransactionId other) => Value.CompareTo(other.Value);

    /// <summary>
    /// Opérateur inférieur.
    /// </summary>
    public static bool operator <(TransactionId left, TransactionId right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Opérateur inférieur ou égal.
    /// </summary>
    public static bool operator <=(TransactionId left, TransactionId right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Opérateur supérieur.
    /// </summary>
    public static bool operator >(TransactionId left, TransactionId right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Opérateur supérieur ou égal.
    /// </summary>
    public static bool operator >=(TransactionId left, TransactionId right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Identifiant typé et validé d'une catégorie budgétaire.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct CategoryId : IComparable<CategoryId>
{
    /// <summary>
    /// Valeur sous-jacente du GUID.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="CategoryId"/> après validation de non-vacuité.
    /// </summary>
    /// <param name="value">GUID non vide.</param>
    /// <exception cref="ArgumentException">Lancée si le GUID est vide.</exception>
    public CategoryId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de catégorie ne peut pas être un GUID vide.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Génère un nouvel identifiant unique de catégorie.
    /// </summary>
    public static CategoryId New() => new(Guid.NewGuid());

    /// <summary>
    /// Tente d'analyser une chaîne GUID en <see cref="CategoryId"/>.
    /// </summary>
    public static bool TryParse(string? input, [NotNullWhen(true)] out CategoryId? id)
    {
        if (Guid.TryParse(input, out var guid) && guid != Guid.Empty)
        {
            id = new CategoryId(guid);
            return true;
        }

        id = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Conversion implicite vers <see cref="Guid"/>.
    /// </summary>
    public static implicit operator Guid(CategoryId id) => id.Value;

    /// <inheritdoc />
    public int CompareTo(CategoryId other) => Value.CompareTo(other.Value);

    /// <summary>
    /// Opérateur inférieur.
    /// </summary>
    public static bool operator <(CategoryId left, CategoryId right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Opérateur inférieur ou égal.
    /// </summary>
    public static bool operator <=(CategoryId left, CategoryId right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Opérateur supérieur.
    /// </summary>
    public static bool operator >(CategoryId left, CategoryId right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Opérateur supérieur ou égal.
    /// </summary>
    public static bool operator >=(CategoryId left, CategoryId right) => left.CompareTo(right) >= 0;
}
