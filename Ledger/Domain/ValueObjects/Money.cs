using System.Globalization;
using System.Runtime.InteropServices;

namespace Ledger.Domain.ValueObjects;

/// <summary>
/// Value Object immuable encapsulant un montant monétaire et sa devise associée,
/// interdisant les opérations arithmétiques inter-devises non converties.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Money : IComparable<Money>, IFormattable
{
    /// <summary>
    /// Montant numérique décimal.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// Code devise ISO-4217 associé.
    /// </summary>
    public CurrencyCode Currency { get; }

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="Money"/>.
    /// </summary>
    /// <param name="amount">Montant décimal.</param>
    /// <param name="currency">Devise monétaire.</param>
    public Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="Money"/> dans la devise par défaut (EUR).
    /// </summary>
    /// <param name="amount">Montant décimal.</param>
    public Money(decimal amount) : this(amount, CurrencyCode.Eur)
    {
    }

    /// <summary>
    /// Crée un montant nul pour la devise spécifiée.
    /// </summary>
    /// <param name="currency">Devise cible.</param>
    /// <returns>Montant de zéro dans la devise spécifiée.</returns>
    public static Money Zero(CurrencyCode currency) => new(0m, currency);

    /// <summary>
    /// Indique si le montant est exactement égal à zéro.
    /// </summary>
    public bool IsZero => Amount == 0m;

    /// <summary>
    /// Indique si le montant est strictement positif.
    /// </summary>
    public bool IsPositive => Amount > 0m;

    /// <summary>
    /// Indique si le montant est strictement négatif.
    /// </summary>
    public bool IsNegative => Amount < 0m;

    /// <summary>
    /// Retourne la valeur absolue du montant dans la même devise.
    /// </summary>
    /// <returns>Nouveau montant absolu.</returns>
    public Money Abs() => new(Math.Abs(Amount), Currency);

    /// <summary>
    /// Additionne deux montants de même devise.
    /// </summary>
    /// <exception cref="InvalidOperationException">Lancée si les deux montants ne partagent pas la même devise.</exception>
    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    /// <summary>
    /// Soustrait deux montants de même devise.
    /// </summary>
    /// <exception cref="InvalidOperationException">Lancée si les deux montants ne partagent pas la même devise.</exception>
    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    /// <summary>
    /// Inverse le signe du montant monétaire.
    /// </summary>
    public static Money operator -(Money money) => new(-money.Amount, money.Currency);

    /// <summary>
    /// Multiplie un montant monétaire par un facteur scalaire.
    /// </summary>
    public static Money operator *(Money money, decimal multiplier) => new(money.Amount * multiplier, money.Currency);

    /// <summary>
    /// Multiplie un montant monétaire par un facteur scalaire.
    /// </summary>
    public static Money operator *(decimal multiplier, Money money) => money * multiplier;

    /// <summary>
    /// Divise un montant monétaire par un diviseur scalaire.
    /// </summary>
    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0m)
        {
            throw new DivideByZeroException("Division d'un montant monétaire par zéro.");
        }
        return new Money(money.Amount / divisor, money.Currency);
    }

    /// <summary>
    /// Compare deux montants monétaires de même devise.
    /// </summary>
    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    /// <summary>
    /// Compare deux montants monétaires de même devise.
    /// </summary>
    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    /// <summary>
    /// Compare deux montants monétaires de même devise.
    /// </summary>
    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    /// <summary>
    /// Compare deux montants monétaires de même devise.
    /// </summary>
    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Opération arithmétique impossible entre devises distinctes ({left.Currency} et {right.Currency}) sans conversion préalable.");
        }
    }

    /// <inheritdoc />
    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Amount.ToString("N2", CultureInfo.CurrentCulture)} {Currency}";

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"{Amount.ToString(format, formatProvider)} {Currency}";
}
