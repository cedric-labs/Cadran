using Ledger.Features.Accounts.Entities;
using Ledger.Features.Imports.Models;

namespace Ledger.Features.Imports.Services;

/// <summary>
/// Construit les transactions persistées à partir des lignes d'un relevé.
/// </summary>
internal static class ImportedTransactionFactory
{
    public static Transaction Create(
        ImportedTransactionDto dto,
        Guid bankAccountId,
        TransactionCustomStatus? defaultStatus,
        IReadOnlyDictionary<string, Guid> categoryIdsByName) =>
        Build(bankAccountId, defaultStatus, categoryIdsByName, importBatchId: null,
            dto.Date, dto.ValueDate, dto.Amount, dto.Type, dto.Label, dto.RawLabel, dto.Reference, dto.CategoryName);

    public static Transaction Create(
        SuspectedDuplicateDto duplicate,
        Guid bankAccountId,
        Guid importBatchId,
        TransactionCustomStatus? defaultStatus,
        IReadOnlyDictionary<string, Guid> categoryIdsByName) =>
        Build(bankAccountId, defaultStatus, categoryIdsByName, importBatchId,
            duplicate.Date, duplicate.ValueDate, duplicate.Amount, duplicate.Type, duplicate.Label, duplicate.RawLabel, duplicate.Reference, duplicate.CategoryName);

    private static Transaction Build(
        Guid bankAccountId,
        TransactionCustomStatus? defaultStatus,
        IReadOnlyDictionary<string, Guid> categoryIdsByName,
        Guid? importBatchId,
        DateTime date,
        DateTime? valueDate,
        decimal amount,
        TransactionType type,
        string label,
        string? rawLabel,
        string? reference,
        string? categoryName)
    {
        Guid? categoryId = !string.IsNullOrEmpty(categoryName) && categoryIdsByName.TryGetValue(categoryName, out var foundId)
            ? foundId
            : null;

        return new Transaction
        {
            UserId = string.Empty, // Auto-rempli par SaveChangesAsync
            BankAccountId = bankAccountId,
            Date = date,
            ValueDate = valueDate,
            Amount = amount,
            Type = type,
            Status = defaultStatus?.Code switch
            {
                "Pending" => TransactionStatus.Pending,
                "Reconciled" => TransactionStatus.Reconciled,
                _ => TransactionStatus.Cleared
            },
            CustomStatusId = defaultStatus?.Id,
            Label = label,
            RawLabel = rawLabel,
            Reference = reference,
            CategoryId = categoryId,
            ImportBatchId = importBatchId
        };
    }
}
