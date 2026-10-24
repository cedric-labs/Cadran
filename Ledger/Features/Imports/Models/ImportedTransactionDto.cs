using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Imports.Models;

/// <summary>
/// DTO représentant une opération extraite d'un relevé bancaire brut (CSV, OFX)
/// avant sa normalisation et persistance dans le domaine.
/// </summary>
public record ImportedTransactionDto
{
    public required DateTime Date { get; init; }
    public DateTime? ValueDate { get; init; }
    public required decimal Amount { get; init; }
    public required TransactionType Type { get; init; }
    public required string Label { get; init; }
    public string? RawLabel { get; init; }
    public string? Reference { get; init; }
    public string? ExternalId { get; init; }
    public string? CategoryName { get; init; }
}
