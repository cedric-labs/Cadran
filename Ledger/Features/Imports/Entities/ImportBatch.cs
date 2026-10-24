using Ledger.Domain.Entities;
using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Imports.Entities;

/// <summary>
/// Journal d'audit d'un lot d'importation de relevé bancaire.
/// </summary>
public class ImportBatch : BaseEntity, IUserOwnedEntity
{
    public required string UserId { get; set; }

    public Guid BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    public required string FileName { get; set; }
    public required string Format { get; set; } // "CSV" ou "OFX"

    public int TotalParsed { get; set; }
    public int TotalImported { get; set; }
    public int TotalDuplicatesSkipped { get; set; }
    public int TotalAutoCategorized { get; set; }
}
