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
    private readonly ImportResultFactory resultFactory = new(localizer);
    private readonly ImportAccountResolver accountResolver = new(dbContext);
    private readonly ImportTransactionScreener screener = new(dbContext, uiSettingsService, new ImportResultFactory(localizer));

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

        var parser = parsers.FirstOrDefault(p => p.CanHandle(fileName, memoryStream));
        memoryStream.Position = 0;
        if (parser == null)
        {
            return resultFactory.NoCompatibleParser(fileName);
        }

        var resolution = await accountResolver.ResolveAsync(parser, memoryStream, bankAccountId, cancellationToken).ConfigureAwait(false);
        if (resolution.Account is not { } account)
        {
            var isOfx = string.Equals(parser.SupportedFormat, "OFX", StringComparison.Ordinal);
            return resultFactory.AccountSelectionRequired(resolution.DetectedOfxAcctId, isOfx);
        }

        var mappingProfile = await FindMappingProfileAsync(csvProfileIdOverride ?? account.CsvMappingProfileId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Models.ImportedTransactionDto> parsedDtos;
        try
        {
            memoryStream.Position = 0;
            parsedDtos = await parser.ParseAsync(memoryStream, mappingProfile, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erreur lors du parsing du relevé bancaire {FileName}", fileName);
            return resultFactory.FileReadError(ex);
        }

        if (parsedDtos.Count == 0)
        {
            return resultFactory.NoValidTransactions();
        }

        var screening = await screener.ScreenAsync(parsedDtos, account.Id, cancellationToken).ConfigureAwait(false);

        // Seules les opérations non catégorisées par le fichier passent par le moteur de règles
        var uncategorized = screening.NewTransactions.Where(t => t.CategoryId == null).ToList();
        await rulesEngineService.ApplyRulesToTransactionsAsync(uncategorized, cancellationToken).ConfigureAwait(false);
        var totalCategorized = screening.NewTransactions.Count(t => t.CategoryId != null);

        var batch = await SaveBatchAsync(account.Id, fileName, parser.SupportedFormat, parsedDtos.Count, screening, totalCategorized, cancellationToken).ConfigureAwait(false);
        return resultFactory.Success(parsedDtos.Count, screening, totalCategorized, batch.Id, account, resolution);
    }

    private async Task<CsvMappingProfile?> FindMappingProfileAsync(Guid? profileId, CancellationToken cancellationToken)
    {
        if (profileId is not { } id)
        {
            return null;
        }

        return await dbContext.CsvMappingProfiles.FindAsync([id], cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImportBatch> SaveBatchAsync(
        Guid bankAccountId,
        string fileName,
        string format,
        int totalParsed,
        ImportScreening screening,
        int totalCategorized,
        CancellationToken cancellationToken)
    {
        var batch = new ImportBatch
        {
            UserId = string.Empty,
            BankAccountId = bankAccountId,
            FileName = fileName,
            Format = format,
            TotalParsed = totalParsed,
            TotalImported = screening.NewTransactions.Count,
            TotalDuplicatesSkipped = screening.ConfirmedDuplicatesCount + screening.SuspectedDuplicates.Count,
            TotalAutoCategorized = totalCategorized
        };

        // Le lot est enregistré en premier pour obtenir l'identifiant que les opérations référencent.
        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (screening.NewTransactions.Count == 0)
        {
            return batch;
        }

        foreach (var transaction in screening.NewTransactions)
        {
            transaction.ImportBatchId = batch.Id;
        }

        dbContext.Transactions.AddRange(screening.NewTransactions);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return batch;
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

        var categoryIdsByName = await dbContext.Categories
            .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken).ConfigureAwait(false);

        var defaultStatus = await dbContext.TransactionStatuses
            .FirstOrDefaultAsync(s => s.IsDefault, cancellationToken).ConfigureAwait(false);

        var newTransactions = confirmedDuplicates
            .Select(duplicate => ImportedTransactionFactory.Create(duplicate, bankAccountId, batchId, defaultStatus, categoryIdsByName))
            .ToList();

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

    private static bool IsTokenSetMatching(HashSet<string> tokensA, HashSet<string> tokensB, bool sameDate) =>
        StatementForge.Analysis.StatementLabelAnalyzer.IsTokenSetMatching(tokensA, tokensB, sameDate);
}
