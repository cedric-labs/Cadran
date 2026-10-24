using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;

namespace Ledger.Features.Reports.Models;

/// <summary>
/// Élément unitaire aplati (transaction parente ou scission enfant) utilisé pour les calculs d'agrégation du tableau croisé dynamique.
/// </summary>
public sealed class PivotTransactionItem
{
    public Guid Id { get; init; }
    public Guid TransactionId { get; init; }
    public DateTime Date { get; init; }
    public DateTime? BudgetDate { get; init; }
    public decimal Amount { get; init; }
    public TransactionType Type { get; init; }
    public Guid BankAccountId { get; init; }
    public BankAccount? BankAccount { get; init; }
    public Guid? CategoryId { get; init; }
    public Category? Category { get; init; }
    public Guid? MerchantId { get; init; }
    public Merchant? Merchant { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? RawLabel { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public bool IsSplitChild { get; init; }
}
