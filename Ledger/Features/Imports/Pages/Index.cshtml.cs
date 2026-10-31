using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Pages;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Imports.Entities;
using Ledger.Features.Imports.Services;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Ledger.Features.Imports.Pages;

[Authorize]
public class IndexModel(
    AppDbContext dbContext,
    ITransactionImportService importService,
    IAccountTypeService accountTypeService,
    Ledger.Features.Settings.Services.IUiSettingsService uiSettingsService,
    IStringLocalizer localizer,
    Ledger.Infrastructure.Configuration.ILocalizedEntityService localizedEntityService,
    ILogger<IndexModel>? logger = null,
    ManualEntryMatchLoader? matchLoader = null) : PageModel
{
    private readonly ILogger<IndexModel> _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<IndexModel>.Instance;

    [BindProperty]
    public UploadInput Input { get; set; } = new();

    public List<BankAccount> AvailableAccounts { get; set; } = new();
    public List<Bank> AvailableBanks { get; set; } = new();
    public List<CustomAccountType> AvailableAccountTypes { get; set; } = new();
    public List<CsvMappingProfile> AvailableProfiles { get; set; } = new();
    public List<ImportBatch> RecentBatches { get; set; } = new();
    public Dictionary<Guid, string> CategoryTranslations { get; set; } = new();

    public ImportResultDto? LastResult { get; set; }
    public string? SerializedSuspectedDuplicates { get; set; }

    // Saisies manuelles qui ressemblent à une opération désormais importée : la fusion se fait sur l'écran de rapprochement.
    public int PendingManualMatchCount { get; set; }

    // Tolérance d'écart de date pour les doublons
    public int ConfiguredToleranceDays { get; set; } = 5;

    // État lorsque le fichier OFX a un <ACCTID> qui ne correspond à aucun compte existant ou aucun compte n'a été spécifié
    public string? UnmatchedOfxAcctId { get; set; }
    public string? PendingOfxFileName { get; set; }
    public string? PendingOfxTempFile { get; set; }
    public bool ShowPendingOfxResolution { get; set; }

    [TempData]
    public string? StatusNotification { get; set; }

    public class UploadInput
    {
        [Display(Name = "Compte bancaire de destination")]
        public Guid? BankAccountId { get; set; }

        [Required(ErrorMessage = "Veuillez sélectionner un fichier bancaire.")]
        [Display(Name = "Fichier bancaire (CSV ou OFX)")]
        public IFormFile? File { get; set; }

        [Display(Name = "Profil de mapping CSV (optionnel si déjà assigné au compte)")]
        public Guid? CsvProfileId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? accountId = null)
    {
        if (accountId.HasValue)
        {
            Input.BankAccountId = accountId.Value;
        }

        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.File == null || Input.File.Length == 0)
        {
            ModelState.AddModelError("Input.File", localizer["Imports.PleaseSelectValidFile"]);
            await LoadDataAsync();
            return Page();
        }

        var isOfx = Input.File.FileName.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase)
                 || Input.File.FileName.EndsWith(".qfx", StringComparison.OrdinalIgnoreCase);

        // Si ce n'est pas un OFX, le compte bancaire est obligatoire
        if (!isOfx && (!Input.BankAccountId.HasValue || Input.BankAccountId.Value == Guid.Empty))
        {
            ModelState.AddModelError("Input.BankAccountId", localizer["Imports.TargetAccountRequiredForCsv"]);
            await LoadDataAsync();
            return Page();
        }

        using var stream = Input.File.OpenReadStream();
        LastResult = await importService.ImportStatementAsync(
            Input.BankAccountId,
            Input.File.FileName,
            stream,
            Input.CsvProfileId);

        // Si l'OFX requiert le choix ou la création d'un compte (pas d'ACCTID correspondant, ou pas de compte spécifié)
        if (!LastResult.Success && LastResult.RequiresAccountSelection)
        {
            ShowPendingOfxResolution = true;
            UnmatchedOfxAcctId = LastResult.DetectedOfxAcctId;
            PendingOfxFileName = Input.File.FileName;

            // Sauvegarder temporairement le fichier dans le dossier temp pour permettre à l'utilisateur
            // de choisir un compte ou d'en créer un nouveau sans devoir ré-uploader le fichier
            var tempDir = Path.Combine(Path.GetTempPath(), "Ledger_Uploads");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, $"{Guid.NewGuid()}_{Input.File.FileName}");
            await using (var fileStream = System.IO.File.Create(tempFile))
            await using (var readStream = Input.File.OpenReadStream())
            {
                await readStream.CopyToAsync(fileStream);
            }
            PendingOfxTempFile = tempFile;
        }

        if (LastResult.Success && matchLoader is not null)
        {
            PendingManualMatchCount = (await matchLoader.LoadAsync()).Count;
        }

        if (LastResult.SuspectedDuplicates != null && LastResult.SuspectedDuplicates.Count > 0)
        {
            SerializedSuspectedDuplicates = JsonSerializer.Serialize(LastResult.SuspectedDuplicates);
        }

        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostProcessPendingOfxAsync(
        string tempFilePath,
        string originalFileName,
        string? detectedOfxAcctId,
        Guid chosenAccountId,
        bool associateOfxIdToAccount)
    {
        if (string.IsNullOrEmpty(tempFilePath) || !System.IO.File.Exists(tempFilePath))
        {
            StatusNotification = localizer["Imports.TempFileExpired"];
            await LoadDataAsync();
            return RedirectToPage();
        }

        if (chosenAccountId == Guid.Empty)
        {
            StatusNotification = localizer["Imports.PleaseChooseAccount"];
            await LoadDataAsync();
            return RedirectToPage();
        }

        // Si l'utilisateur demande d'associer l'<ACCTID> à ce compte pour les prochains imports
        if (associateOfxIdToAccount && !string.IsNullOrEmpty(detectedOfxAcctId))
        {
            var acc = await dbContext.BankAccounts.FirstOrDefaultAsync(a => a.Id == chosenAccountId);
            if (acc != null && string.IsNullOrEmpty(acc.OfxAccountId))
            {
                acc.OfxAccountId = detectedOfxAcctId.Trim();
                await dbContext.SaveChangesAsync();
            }
        }

        await ImportStagedFileAsync(chosenAccountId, originalFileName, tempFilePath);

        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAccountAndImportAsync(
        string tempFilePath,
        string originalFileName,
        string? detectedOfxAcctId,
        Guid? existingBankId,
        string? newBankName,
        string accountName,
        Guid? customAccountTypeId,
        decimal initialBalance)
    {
        if (string.IsNullOrEmpty(tempFilePath) || !System.IO.File.Exists(tempFilePath))
        {
            StatusNotification = localizer["Imports.TempFileExpired"];
            await LoadDataAsync();
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(accountName))
        {
            StatusNotification = localizer["Imports.PleaseEnterAccountName"];
            await LoadDataAsync();
            return RedirectToPage();
        }

        Guid targetBankId;
        if (existingBankId.HasValue && existingBankId.Value != Guid.Empty)
        {
            targetBankId = existingBankId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(newBankName))
        {
            var newBank = new Bank
            {
                UserId = string.Empty, // Injecté par SaveChangesAsync
                Name = newBankName.Trim()
            };
            dbContext.Banks.Add(newBank);
            await dbContext.SaveChangesAsync();
            targetBankId = newBank.Id;
        }
        else
        {
            StatusNotification = localizer["Imports.PleaseChooseOrEnterBank"];
            await LoadDataAsync();
            return RedirectToPage();
        }

        var legacyType = await LegacyAccountTypeResolver.ResolveAsync(accountTypeService, customAccountTypeId, AccountType.Checking, CancellationToken.None);

        var newAccount = new BankAccount
        {
            UserId = string.Empty,
            BankId = targetBankId,
            Name = accountName.Trim(),
            CustomAccountTypeId = customAccountTypeId,
            Type = legacyType,
            OfxAccountId = string.IsNullOrWhiteSpace(detectedOfxAcctId) ? null : detectedOfxAcctId.Trim(),
            InitialBalance = initialBalance,
            Currency = "EUR"
        };

        dbContext.BankAccounts.Add(newAccount);
        await dbContext.SaveChangesAsync();

        await ImportStagedFileAsync(newAccount.Id, originalFileName, tempFilePath);

        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmDuplicatesAsync(
        Guid batchId,
        Guid bankAccountId,
        string? serializedDuplicates,
        List<int> selectedIndices)
    {
        if (string.IsNullOrWhiteSpace(serializedDuplicates) || selectedIndices == null || selectedIndices.Count == 0)
        {
            StatusNotification = localizer["Imports.NoSuspectsSelected"];
            await LoadDataAsync();
            return Page();
        }

        try
        {
            var allSuspected = JsonSerializer.Deserialize<List<SuspectedDuplicateDto>>(serializedDuplicates);
            if (allSuspected != null)
            {
                var toImport = allSuspected.Where(d => selectedIndices.Contains(d.Index)).ToList();
                int imported = await importService.ConfirmDuplicatesAsync(batchId, bankAccountId, toImport);
                StatusNotification = string.Format(CultureInfo.CurrentCulture, localizer["Imports.ConfirmedDuplicatesSuccess"], imported);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Erreur de désérialisation JSON lors de la confirmation des doublons suspects.");
            StatusNotification = string.Format(CultureInfo.CurrentCulture, localizer["Imports.ConfirmDuplicatesError"], ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erreur d'opération lors de la confirmation des doublons suspects.");
            StatusNotification = string.Format(CultureInfo.CurrentCulture, localizer["Imports.ConfirmDuplicatesError"], ex.Message);
        }

        await LoadDataAsync();
        return Page();
    }

    // Gestion des doublons existants en base
    public Guid? SelectedDeduplicateAccountId { get; set; }
    public IReadOnlyList<ExistingDuplicateGroupDto>? ExistingDuplicates { get; set; }

    public async Task<IActionResult> OnPostFindDuplicatesAsync(Guid deduplicateAccountId)
    {
        if (deduplicateAccountId == Guid.Empty)
        {
            StatusNotification = localizer["Imports.PleaseSelectAccountToAnalyze"];
            await LoadDataAsync();
            return Page();
        }

        SelectedDeduplicateAccountId = deduplicateAccountId;
        ExistingDuplicates = await importService.FindExistingDuplicatesAsync(deduplicateAccountId);

        if (ExistingDuplicates.Count == 0)
        {
            StatusNotification = localizer["Imports.NoDuplicatesDetectedOnAccount"];
        }

        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteDuplicatesAsync(Guid deduplicateAccountId, List<Guid> transactionIdsToDelete)
    {
        if (deduplicateAccountId == Guid.Empty || transactionIdsToDelete == null || transactionIdsToDelete.Count == 0)
        {
            StatusNotification = localizer["Imports.NoTransactionsSelectedForDeletion"];
            await LoadDataAsync();
            return Page();
        }

        int deletedCount = await importService.DeleteExistingDuplicatesAsync(deduplicateAccountId, transactionIdsToDelete);
        StatusNotification = string.Format(CultureInfo.CurrentCulture, localizer["Imports.DeletedDuplicatesSuccess"], deletedCount);

        // Réactualiser la liste des doublons restants
        SelectedDeduplicateAccountId = deduplicateAccountId;
        ExistingDuplicates = await importService.FindExistingDuplicatesAsync(deduplicateAccountId);

        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteBatchAsync(Guid batchId)
    {
        var deleted = await importService.DeleteBatchAsync(batchId);
        if (deleted)
        {
            StatusNotification = localizer["Imports.DeletedBatchSuccess"];
        }
        else
        {
            StatusNotification = localizer["Imports.BatchNotFound"];
        }

        await LoadDataAsync();
        return Page();
    }

    private async Task LoadDataAsync()
    {
        AvailableAccounts = await dbContext.BankAccounts
            .Include(a => a.Bank)
            .OrderBy(a => a.Name)
            .ToListAsync();

        AvailableBanks = await dbContext.Banks
            .OrderBy(b => b.Name)
            .ToListAsync();

        AvailableAccountTypes = await accountTypeService.GetAllAccountTypesAsync();

        AvailableProfiles = await dbContext.CsvMappingProfiles
            .OrderBy(p => p.Name)
            .ToListAsync();

        RecentBatches = await dbContext.ImportBatches
            .Include(b => b.BankAccount)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Take(10)
            .ToListAsync();

        ConfiguredToleranceDays = await uiSettingsService.GetDuplicateToleranceDaysAsync();

        if (ExistingDuplicates != null && ExistingDuplicates.Count > 0)
        {
            var catIds = ExistingDuplicates
                .SelectMany(g => g.Transactions)
                .Select(t => t.Category)
                .OfType<Ledger.Features.Categories.Entities.Category>()
                .Select(c => c.Id)
                .Distinct()
                .ToList();

            if (catIds.Count > 0)
            {
                CategoryTranslations = await localizedEntityService.GetTranslationsAsync("Category", catIds, "Name");
            }
        }
    }

    // Importe le fichier déposé, le supprime ensuite et mémorise les doublons suspects pour la confirmation de l'utilisateur.
    private async Task ImportStagedFileAsync(Guid accountId, string originalFileName, string tempFilePath)
    {
        using (var stream = System.IO.File.OpenRead(tempFilePath))
        {
            LastResult = await importService.ImportStatementAsync(accountId, originalFileName, stream);
        }

        try
        {
            System.IO.File.Delete(tempFilePath);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Le fichier temporaire '{TempFilePath}' n'a pas pu être supprimé immédiatement.", tempFilePath);
        }

        if (LastResult.SuspectedDuplicates != null && LastResult.SuspectedDuplicates.Any())
        {
            SerializedSuspectedDuplicates = JsonSerializer.Serialize(LastResult.SuspectedDuplicates);
        }
    }
}
