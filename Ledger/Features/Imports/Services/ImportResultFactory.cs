using System.Globalization;
using Ledger.Features.Accounts.Entities;
using Microsoft.Extensions.Localization;

namespace Ledger.Features.Imports.Services;

/// <summary>
/// Produit les résultats d'import et les libellés localisés qui les accompagnent.
/// </summary>
internal sealed class ImportResultFactory(IStringLocalizer localizer)
{
    public ImportResultDto NoCompatibleParser(string fileName) =>
        Failure(string.Format(CultureInfo.CurrentCulture, localizer["Imports.NoCompatibleParserFound"], fileName));

    public ImportResultDto FileReadError(Exception exception) =>
        Failure(string.Format(CultureInfo.CurrentCulture, localizer["Imports.FileReadError"], exception.Message));

    public ImportResultDto NoValidTransactions() =>
        new(true, localizer["Imports.NoValidTransactionsDetected"], 0, 0, 0, 0, null);

    public ImportResultDto AccountSelectionRequired(string? detectedOfxAcctId, bool isOfx)
    {
        if (!string.IsNullOrEmpty(detectedOfxAcctId))
        {
            return new ImportResultDto(
                Success: false,
                Message: string.Format(CultureInfo.CurrentCulture, localizer["Imports.OfxAcctIdNoMatchMessage"], detectedOfxAcctId),
                TotalParsed: 0,
                TotalImported: 0,
                TotalDuplicatesSkipped: 0,
                TotalAutoCategorized: 0,
                BatchId: null,
                DetectedOfxAcctId: detectedOfxAcctId,
                RequiresAccountSelection: true);
        }

        return new ImportResultDto(
            Success: false,
            Message: localizer["Imports.NoMatchingAccountFound"],
            TotalParsed: 0,
            TotalImported: 0,
            TotalDuplicatesSkipped: 0,
            TotalAutoCategorized: 0,
            BatchId: null,
            RequiresAccountSelection: isOfx);
    }

    public ImportResultDto Success(
        int totalParsed,
        ImportScreening screening,
        int totalCategorized,
        Guid batchId,
        BankAccount account,
        ImportAccountResolution resolution)
    {
        var accountName = $"{account.Bank?.Name} - {account.Name}";
        var message = resolution.AutoMatchedByOfx
            ? string.Format(CultureInfo.CurrentCulture, localizer["Imports.OfxAutoMatchedMessage"], accountName, resolution.DetectedOfxAcctId)
            : string.Format(CultureInfo.CurrentCulture, localizer["Imports.AccountImportMessage"], accountName);

        message += screening.SuspectedDuplicates.Count > 0
            ? string.Format(CultureInfo.CurrentCulture, localizer["Imports.ImportWithDuplicatesMessage"], screening.NewTransactions.Count, screening.SuspectedDuplicates.Count)
            : string.Format(CultureInfo.CurrentCulture, localizer["Imports.ImportSuccessMessage"], screening.NewTransactions.Count, screening.ConfirmedDuplicatesCount, totalCategorized);

        return new ImportResultDto(
            Success: true,
            Message: message,
            TotalParsed: totalParsed,
            TotalImported: screening.NewTransactions.Count,
            TotalDuplicatesSkipped: screening.ConfirmedDuplicatesCount,
            TotalAutoCategorized: totalCategorized,
            BatchId: batchId,
            SuspectedDuplicates: screening.SuspectedDuplicates,
            DetectedOfxAcctId: resolution.DetectedOfxAcctId,
            TargetBankAccountId: account.Id,
            TargetBankAccountName: accountName,
            AutoMatchedByOfx: resolution.AutoMatchedByOfx);
    }

    public string DescribeMatch(ImportDuplicateVerdict verdict) =>
        verdict.ExistingMatch is { } existing
            ? DescribeExistingMatch(existing)
            : DescribeBatchMatch(verdict.BatchMatch);

    private string DescribeExistingMatch(Transaction existing)
    {
        var referenceSuffix = string.IsNullOrEmpty(existing.Reference)
            ? ""
            : " | " + string.Format(CultureInfo.CurrentCulture, localizer["Imports.BankRefPrefix"], existing.Reference);
        return string.Format(
            CultureInfo.CurrentCulture,
            localizer["Imports.ExistingMatchDesc"],
            existing.Label,
            existing.Date.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture),
            existing.Amount.ToString("C2", CultureInfo.CurrentCulture) + referenceSuffix);
    }

    private string DescribeBatchMatch(AcceptedImportRow? batchRow)
    {
        ArgumentNullException.ThrowIfNull(batchRow);
        return string.Format(
            CultureInfo.CurrentCulture,
            localizer["Imports.BatchMatchDesc"],
            batchRow.Label,
            batchRow.Date.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture));
    }

    private static ImportResultDto Failure(string message) => new(false, message, 0, 0, 0, 0, null);
}
