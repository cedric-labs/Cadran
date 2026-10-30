using System.Globalization;
using System.Text.RegularExpressions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Imports.Entities;
using Ledger.Features.Imports.Strategies;
using Ledger.Features.RulesEngine.Services;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Ledger.Features.Imports.Services;

/// <summary>
/// Service d'orchestration de l'importation de relevés bancaires.
/// Exploite le Pattern Strategy pour parser le fichier, vérifie les doublons avec arbitrage utilisateur,
/// déclenche le moteur de règles pour catégoriser automatiquement les opérations
/// et garantit l'intégrité transactionnelle.
/// </summary>
public sealed class TransactionImportService(
    IEnumerable<IBankStatementParser> parsers,
    AppDbContext dbContext,
    IRulesEngineService rulesEngineService,
    Ledger.Features.Settings.Services.IUiSettingsService uiSettingsService,
    ILogger<TransactionImportService> logger,
    IStringLocalizer localizer) : ITransactionImportService
{

    public async Task<ImportResultDto> ImportStatementAsync(
        Guid? bankAccountId,
        string fileName,
        Stream fileStream,
        Guid? csvProfileIdOverride = null,
        CancellationToken cancellationToken = default)
    {
        // Copier dans un MemoryStream pour garantir la possibilité de relecture (rewind / seek)
        // et éviter l'épuisement de flux non-seekable issus de requêtes HTTP
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;

        // 1. Sélection de la stratégie de parsing (Pattern Strategy)
        var parser = parsers.FirstOrDefault(p => p.CanHandle(fileName, memoryStream));
        memoryStream.Position = 0;

        if (parser == null)
        {
            return new ImportResultDto(false, string.Format(CultureInfo.CurrentCulture, localizer["Imports.NoCompatibleParserFound"], fileName), 0, 0, 0, 0, null);
        }

        // 2. Détection de l'identifiant OFX <ACCTID> si le format est OFX
        string? detectedOfxAcctId = null;
        bool autoMatchedByOfx = false;
        if (string.Equals(parser.SupportedFormat, "OFX", StringComparison.Ordinal))
        {
            detectedOfxAcctId = await OfxStatementParser.ExtractOfxAccountIdAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            memoryStream.Position = 0;
        }

        BankAccount? account = null;

        // Si un ACCTID est détecté dans le fichier OFX, on recherche un compte configuré avec cet identifiant
        if (!string.IsNullOrEmpty(detectedOfxAcctId))
        {
            // Recherche par correspondance exacte ou normalisée (sans espaces/tirets)
            var cleanAcctId = Regex.Replace(detectedOfxAcctId, @"[\s\-]", "", RegexOptions.None, TimeSpan.FromMilliseconds(250));
            var allAccounts = await dbContext.BankAccounts
                .Include(a => a.Bank)
                .Where(a => !a.IsArchived)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var matchedAccount = allAccounts.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.OfxAccountId) &&
                (string.Equals(a.OfxAccountId.Trim(), detectedOfxAcctId, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Regex.Replace(a.OfxAccountId, @"[\s\-]", "", RegexOptions.None, TimeSpan.FromMilliseconds(250)), cleanAcctId, StringComparison.OrdinalIgnoreCase)));

            if (matchedAccount != null)
            {
                account = matchedAccount;
                bankAccountId = matchedAccount.Id;
                autoMatchedByOfx = true;
            }
        }

        // Si non apparié par ACCTID et qu'un compte a été sélectionné manuellement
        if (account == null && bankAccountId.HasValue && bankAccountId.Value != Guid.Empty)
        {
            account = await dbContext.BankAccounts
                .Include(a => a.Bank)
                .FirstOrDefaultAsync(a => a.Id == bankAccountId.Value, cancellationToken).ConfigureAwait(false);
        }

        // Si aucun compte n'est trouvé ou sélectionné
        if (account == null)
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

            var isOfx = string.Equals(parser.SupportedFormat, "OFX", StringComparison.Ordinal);
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

        var effectiveBankAccountId = account.Id;

        // 3. Récupération du profil de mapping CSV
        CsvMappingProfile? mappingProfile = null;
        var targetProfileId = csvProfileIdOverride ?? account.CsvMappingProfileId;
        if (targetProfileId.HasValue)
        {
            mappingProfile = await dbContext.CsvMappingProfiles.FindAsync([targetProfileId.Value], cancellationToken).ConfigureAwait(false);
        }

        // 4. Exécution du parsing
        IReadOnlyList<Models.ImportedTransactionDto> parsedDtos;
        try
        {
            memoryStream.Position = 0;
            parsedDtos = await parser.ParseAsync(memoryStream, mappingProfile, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du parsing du relevé bancaire {FileName}", fileName);
            return new ImportResultDto(false, string.Format(CultureInfo.CurrentCulture, localizer["Imports.FileReadError"], ex.Message), 0, 0, 0, 0, null);
        }

        if (parsedDtos.Count == 0)
        {
            return new ImportResultDto(true, localizer["Imports.NoValidTransactionsDetected"], 0, 0, 0, 0, null);
        }

        // 5. Détection intelligente des doublons
        var minDate = parsedDtos.Min(p => p.Date).AddDays(-3);
        var maxDate = parsedDtos.Max(p => p.Date).AddDays(3);

        var existingTransactions = await dbContext.Transactions
            .Where(t => t.BankAccountId == effectiveBankAccountId && t.Date >= minDate && t.Date <= maxDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var newTransactions = new List<Transaction>();
        var suspectedDuplicates = new List<SuspectedDuplicateDto>();
        int confirmedDuplicatesCount = 0;

        var batchAcceptedReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batchAcceptedRows = new List<(DateTime Date, decimal Amount, TransactionType Type, string Label, HashSet<string> Tokens)>();

        var existingCategories = await dbContext.Categories
            .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken).ConfigureAwait(false);

        var defaultStatus = await dbContext.TransactionStatuses
            .FirstOrDefaultAsync(s => s.IsDefault, cancellationToken).ConfigureAwait(false);

        var toleranceDays = await uiSettingsService.GetDuplicateToleranceDaysAsync(cancellationToken).ConfigureAwait(false);

        int indexCounter = 0;
        foreach (var dto in parsedDtos)
        {
            // 1. Règle absolue : Doublon strict de référence (même fichier réimporté ou export de la même banque)
            if (!string.IsNullOrEmpty(dto.Reference))
            {
                bool isDuplicateFitId = existingTransactions.Any(t => string.Equals(t.Reference, dto.Reference, StringComparison.OrdinalIgnoreCase))
                                     || batchAcceptedReferences.Contains(dto.Reference);

                if (isDuplicateFitId)
                {
                    confirmedDuplicatesCount++;
                    continue;
                }
            }

            // 2. Détection intelligente de doublon multi-sources (Même montant, date identique ou proche +/- toleranceDays jours, libellés similaires)
            var dtoTokens = ExtractMeaningfulTokens(dto.Label, dto.RawLabel);

            // Recherche parmi les opérations déjà enregistrées en base
            var matchingExisting = existingTransactions.FirstOrDefault(t =>
                t.Amount == dto.Amount &&
                t.Type == dto.Type &&
                Math.Abs((t.Date.Date - dto.Date.Date).TotalDays) <= toleranceDays &&
                IsLabelMatching(dtoTokens, t.Label, t.RawLabel, dto.Date.Date == t.Date.Date));

            // Recherche parmi les opérations acceptées plus tôt dans le même fichier
            var matchingBatch = batchAcceptedRows.FirstOrDefault(r =>
                r.Amount == dto.Amount &&
                r.Type == dto.Type &&
                Math.Abs((r.Date.Date - dto.Date.Date).TotalDays) <= toleranceDays &&
                IsTokenSetMatching(dtoTokens, r.Tokens, dto.Date.Date == r.Date.Date));

            if (matchingExisting != null || matchingBatch != default)
            {
                string matchDesc;
                if (matchingExisting != null)
                {
                    var refSuffix = string.IsNullOrEmpty(matchingExisting.Reference) ? "" : " | " + string.Format(CultureInfo.CurrentCulture, localizer["Imports.BankRefPrefix"], matchingExisting.Reference);
                    matchDesc = string.Format(CultureInfo.CurrentCulture, localizer["Imports.ExistingMatchDesc"], matchingExisting.Label, matchingExisting.Date.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture), matchingExisting.Amount.ToString("C2", CultureInfo.CurrentCulture) + refSuffix);
                }
                else
                {
                    matchDesc = string.Format(CultureInfo.CurrentCulture, localizer["Imports.BatchMatchDesc"], matchingBatch.Label, matchingBatch.Date.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture));
                }

                suspectedDuplicates.Add(new SuspectedDuplicateDto(
                    Index: indexCounter++,
                    Date: dto.Date,
                    ValueDate: dto.ValueDate,
                    Amount: dto.Amount,
                    Type: dto.Type,
                    Label: dto.Label,
                    RawLabel: dto.RawLabel,
                    ExistingMatchLabel: matchDesc,
                    Reference: dto.Reference,
                    CategoryName: dto.CategoryName
                ));
                continue;
            }

            if (!string.IsNullOrEmpty(dto.Reference))
            {
                batchAcceptedReferences.Add(dto.Reference);
            }
            batchAcceptedRows.Add((dto.Date, dto.Amount, dto.Type, dto.Label, dtoTokens));

            Guid? directCategoryId = null;
            if (!string.IsNullOrEmpty(dto.CategoryName) && existingCategories.TryGetValue(dto.CategoryName, out var foundCatId))
            {
                directCategoryId = foundCatId;
            }

            var transaction = new Transaction
            {
                UserId = string.Empty, // Auto-rempli par SaveChangesAsync
                BankAccountId = effectiveBankAccountId,
                Date = dto.Date,
                ValueDate = dto.ValueDate,
                Amount = dto.Amount,
                Type = dto.Type,
                Status = defaultStatus?.Code switch
                {
                    "Pending" => TransactionStatus.Pending,
                    "Reconciled" => TransactionStatus.Reconciled,
                    _ => TransactionStatus.Cleared
                },
                CustomStatusId = defaultStatus?.Id,
                Label = dto.Label,
                RawLabel = dto.RawLabel,
                Reference = dto.Reference,
                CategoryId = directCategoryId
            };

            newTransactions.Add(transaction);
        }

        // 6. Application du Moteur de Règles de catégorisation automatique pour les transactions non encore catégorisées
        var uncategorized = newTransactions.Where(t => t.CategoryId == null).ToList();
        await rulesEngineService.ApplyRulesToTransactionsAsync(uncategorized, cancellationToken).ConfigureAwait(false);
        int totalCategorized = newTransactions.Count(t => t.CategoryId != null);

        // 7. Enregistrement en base de données et traçabilité
        var batch = new ImportBatch
        {
            UserId = string.Empty,
            BankAccountId = effectiveBankAccountId,
            FileName = fileName,
            Format = parser.SupportedFormat,
            TotalParsed = parsedDtos.Count,
            TotalImported = newTransactions.Count,
            TotalDuplicatesSkipped = confirmedDuplicatesCount + suspectedDuplicates.Count,
            TotalAutoCategorized = totalCategorized
        };

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var tx in newTransactions)
        {
            tx.ImportBatchId = batch.Id;
        }

        if (newTransactions.Count > 0)
        {
            dbContext.Transactions.AddRange(newTransactions);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        string accountName = $"{account.Bank?.Name} - {account.Name}";
        string message;
        if (autoMatchedByOfx)
        {
            message = string.Format(CultureInfo.CurrentCulture, localizer["Imports.OfxAutoMatchedMessage"], accountName, detectedOfxAcctId);
        }
        else
        {
            message = string.Format(CultureInfo.CurrentCulture, localizer["Imports.AccountImportMessage"], accountName);
        }

        if (suspectedDuplicates.Count > 0)
        {
            message += string.Format(CultureInfo.CurrentCulture, localizer["Imports.ImportWithDuplicatesMessage"], newTransactions.Count, suspectedDuplicates.Count);
        }
        else
        {
            message += string.Format(CultureInfo.CurrentCulture, localizer["Imports.ImportSuccessMessage"], newTransactions.Count, confirmedDuplicatesCount, totalCategorized);
        }

        return new ImportResultDto(
            Success: true,
            Message: message,
            TotalParsed: parsedDtos.Count,
            TotalImported: newTransactions.Count,
            TotalDuplicatesSkipped: confirmedDuplicatesCount,
            TotalAutoCategorized: totalCategorized,
            BatchId: batch.Id,
            SuspectedDuplicates: suspectedDuplicates,
            DetectedOfxAcctId: detectedOfxAcctId,
            TargetBankAccountId: effectiveBankAccountId,
            TargetBankAccountName: accountName,
            AutoMatchedByOfx: autoMatchedByOfx);
    }

    public async Task<int> ConfirmDuplicatesAsync(
        Guid batchId,
        Guid bankAccountId,
        IReadOnlyList<SuspectedDuplicateDto> confirmedDuplicates,
        CancellationToken cancellationToken = default)
    {
        if (confirmedDuplicates == null || confirmedDuplicates.Count == 0)
        {
            return 0;
        }

        var batch = await dbContext.ImportBatches
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken).ConfigureAwait(false);

        var existingCategories = await dbContext.Categories
            .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken).ConfigureAwait(false);

        var defaultStatus = await dbContext.TransactionStatuses
            .FirstOrDefaultAsync(s => s.IsDefault, cancellationToken).ConfigureAwait(false);

        var newTransactions = new List<Transaction>();
        foreach (var dto in confirmedDuplicates)
        {
            Guid? directCatId = null;
            if (!string.IsNullOrEmpty(dto.CategoryName) && existingCategories.TryGetValue(dto.CategoryName, out var foundCatId))
            {
                directCatId = foundCatId;
            }

            var tx = new Transaction
            {
                UserId = string.Empty,
                BankAccountId = bankAccountId,
                Date = dto.Date,
                ValueDate = dto.ValueDate,
                Amount = dto.Amount,
                Type = dto.Type,
                Status = defaultStatus?.Code switch
                {
                    "Pending" => TransactionStatus.Pending,
                    "Reconciled" => TransactionStatus.Reconciled,
                    _ => TransactionStatus.Cleared
                },
                CustomStatusId = defaultStatus?.Id,
                Label = dto.Label,
                RawLabel = dto.RawLabel,
                Reference = dto.Reference,
                CategoryId = directCatId,
                ImportBatchId = batchId
            };
            newTransactions.Add(tx);
        }

        int categorizedCount = await rulesEngineService.ApplyRulesToTransactionsAsync(newTransactions, cancellationToken).ConfigureAwait(false);

        dbContext.Transactions.AddRange(newTransactions);

        if (batch != null)
        {
            batch.TotalImported += newTransactions.Count;
            batch.TotalDuplicatesSkipped = Math.Max(0, batch.TotalDuplicatesSkipped - newTransactions.Count);
            batch.TotalAutoCategorized += categorizedCount;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return newTransactions.Count;
    }

    public async Task<bool> DeleteBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await dbContext.ImportBatches
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken).ConfigureAwait(false);

        if (batch == null)
        {
            return false;
        }

        var transactions = await dbContext.Transactions
            .Where(t => t.ImportBatchId == batchId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        dbContext.Transactions.RemoveRange(transactions);
        dbContext.ImportBatches.Remove(batch);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<IReadOnlyList<ExistingDuplicateGroupDto>> FindExistingDuplicatesAsync(
        Guid bankAccountId,
        CancellationToken cancellationToken = default)
    {
        var transactions = await dbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.CustomStatus)
            .Where(t => t.BankAccountId == bankAccountId)
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var groups = new List<ExistingDuplicateGroupDto>();
        var processed = new HashSet<Guid>();

        // 1. Passe 1 : Détection des doublons sur le même jour (Date exacte, même Montant et Type)
        var byExactDateAmount = transactions
            .GroupBy(t => (t.Date.Date, t.Amount, t.Type));

        foreach (var grp in byExactDateAmount)
        {
            if (grp.Count() <= 1) continue;

            var list = grp.Where(t => !processed.Contains(t.Id)).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (processed.Contains(list[i].Id)) continue;

                var cluster = new List<Transaction> { list[i] };
                var tokensI = ExtractMeaningfulTokens(list[i].Label, list[i].RawLabel);

                for (int j = i + 1; j < list.Count; j++)
                {
                    if (processed.Contains(list[j].Id)) continue;

                    var tokensJ = ExtractMeaningfulTokens(list[j].Label, list[j].RawLabel);
                    if (IsTokenSetMatching(tokensI, tokensJ, sameDate: true))
                    {
                        cluster.Add(list[j]);
                        processed.Add(list[j].Id);
                    }
                }

                if (cluster.Count > 1)
                {
                    processed.Add(list[i].Id);
                    var key = $"{grp.Key.Date:yyyyMMdd}_{grp.Key.Amount}_{grp.Key.Type}_{cluster[0].Id}";
                    groups.Add(new ExistingDuplicateGroupDto(key, grp.Key.Date, grp.Key.Amount, grp.Key.Type, cluster));
                }
            }
        }

        // 2. Passe 2 : Détection des doublons potentiels à dates proches selon la tolérance configurée en base (ou minimum 5 jours)
        var toleranceDays = await uiSettingsService.GetDuplicateToleranceDaysAsync(cancellationToken).ConfigureAwait(false);
        var clusterTolerance = Math.Max(toleranceDays, 5);

        var byAmountAndType = transactions
            .Where(t => !processed.Contains(t.Id))
            .GroupBy(t => (t.Amount, t.Type));

        foreach (var grp in byAmountAndType)
        {
            if (grp.Count() <= 1) continue;

            var list = grp.OrderBy(t => t.Date).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (processed.Contains(list[i].Id)) continue;

                var cluster = new List<Transaction> { list[i] };
                var tokensI = ExtractMeaningfulTokens(list[i].Label, list[i].RawLabel);

                for (int j = i + 1; j < list.Count; j++)
                {
                    if (processed.Contains(list[j].Id)) continue;

                    var daysDiff = Math.Abs((list[j].Date.Date - list[i].Date.Date).TotalDays);
                    if (daysDiff <= clusterTolerance)
                    {
                        var tokensJ = ExtractMeaningfulTokens(list[j].Label, list[j].RawLabel);
                        if (IsTokenSetMatching(tokensI, tokensJ, sameDate: false))
                        {
                            cluster.Add(list[j]);
                            processed.Add(list[j].Id);
                        }
                    }
                }

                if (cluster.Count > 1)
                {
                    processed.Add(list[i].Id);
                    var key = $"{cluster[0].Date:yyyyMMdd}_{grp.Key.Amount}_{grp.Key.Type}_{cluster[0].Id}";
                    groups.Add(new ExistingDuplicateGroupDto(key, cluster[0].Date, grp.Key.Amount, grp.Key.Type, cluster));
                }
            }
        }

        return groups.OrderByDescending(g => g.Date).ToList();
    }

    public async Task<int> DeleteExistingDuplicatesAsync(
        Guid bankAccountId,
        IReadOnlyList<Guid> transactionIdsToDelete,
        CancellationToken cancellationToken = default)
    {
        if (transactionIdsToDelete == null || transactionIdsToDelete.Count == 0)
        {
            return 0;
        }

        var toDelete = await dbContext.Transactions
            .Where(t => t.BankAccountId == bankAccountId && transactionIdsToDelete.Contains(t.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (toDelete.Count > 0)
        {
            dbContext.Transactions.RemoveRange(toDelete);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return toDelete.Count;
    }

    private static HashSet<string> ExtractMeaningfulTokens(string? label, string? rawLabel) =>
        StatementForge.Analysis.StatementLabelAnalyzer.ExtractMeaningfulTokens(label, rawLabel);

    private static bool IsLabelMatching(HashSet<string> dtoTokens, string? existingLabel, string? existingRawLabel, bool sameDate) =>
        StatementForge.Analysis.StatementLabelAnalyzer.IsLabelMatching(dtoTokens, existingLabel, existingRawLabel, sameDate);

    private static bool IsTokenSetMatching(HashSet<string> tokensA, HashSet<string> tokensB, bool sameDate) =>
        StatementForge.Analysis.StatementLabelAnalyzer.IsTokenSetMatching(tokensA, tokensB, sameDate);
}
