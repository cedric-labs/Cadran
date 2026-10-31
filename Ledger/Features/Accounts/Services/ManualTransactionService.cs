using Ledger.Features.Accounts.Entities;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Données saisies pour créer une opération à la main. Le montant est saisi en valeur absolue ; le sens est porté par <see cref="Type"/>.
/// </summary>
public sealed record ManualTransactionRequest(
    Guid BankAccountId,
    DateTime Date,
    string? Label,
    decimal Amount,
    TransactionType Type,
    Guid? CategoryId = null,
    string? Notes = null);

/// <summary>
/// Motif de refus d'une création manuelle.
/// </summary>
public enum ManualTransactionError
{
    None = 0,
    AccountNotFound,
    InvalidAmount,
    InvalidDate,
    LabelRequired,
    UnsupportedType
}

/// <summary>
/// Résultat d'une création manuelle : l'opération créée, ou le motif du refus.
/// </summary>
public sealed record ManualTransactionResult(Transaction? Transaction, ManualTransactionError Error)
{
    public bool Succeeded => Transaction is not null;
}

/// <summary>
/// Crée les opérations saisies à la main (<see cref="TransactionOrigin.Manual"/>).
/// </summary>
public sealed class ManualTransactionService(AppDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<ManualTransactionResult> CreateAsync(ManualTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var error = Validate(request, out var label);
        if (error != ManualTransactionError.None)
        {
            return new ManualTransactionResult(null, error);
        }

        var accountExists = await dbContext.BankAccounts
            .AnyAsync(a => a.Id == request.BankAccountId, cancellationToken)
            .ConfigureAwait(false);
        if (!accountExists)
        {
            return new ManualTransactionResult(null, ManualTransactionError.AccountNotFound);
        }

        var today = timeProvider.GetLocalNow().Date;

        // Une opération datée dans le futur n'est pas encore passée en compte : elle reste « en attente » jusqu'à l'import du relevé.
        var transaction = new Transaction
        {
            UserId = string.Empty,
            BankAccountId = request.BankAccountId,
            Date = request.Date,
            Amount = request.Amount,
            Type = request.Type,
            Label = label,
            CategoryId = request.CategoryId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = request.Date.Date > today ? TransactionStatus.Pending : TransactionStatus.Cleared,
            Origin = TransactionOrigin.Manual
        };

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ManualTransactionResult(transaction, ManualTransactionError.None);
    }

    private static ManualTransactionError Validate(ManualTransactionRequest request, out string label)
    {
        label = request.Label?.Trim() ?? string.Empty;
        if (label.Length == 0)
        {
            return ManualTransactionError.LabelRequired;
        }

        if (request.Amount <= 0)
        {
            return ManualTransactionError.InvalidAmount;
        }

        if (request.Date == default)
        {
            return ManualTransactionError.InvalidDate;
        }

        return request.Type is TransactionType.Debit or TransactionType.Credit
            ? ManualTransactionError.None
            : ManualTransactionError.UnsupportedType;
    }
}
