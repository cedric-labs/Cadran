using System.ComponentModel.DataAnnotations;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ledger.Features.Settings.Pages;

[Authorize]
public class AppearanceModel(IUiSettingsService uiSettingsService) : PageModel
{
    public List<UiFont> Fonts { get; set; } = new();
    public UiFont ActiveFont { get; set; } = new() { Name = string.Empty, FontFamilyCss = string.Empty };
    public string ActiveFontSize { get; set; } = "14px";
    public string DefaultDisplayMode { get; set; } = "compact";
    public string DefaultPeriod { get; set; } = "current-month";
    public int TransactionsPageSize { get; set; } = 250;
    public bool ShowKpiCents { get; set; } = true;
    public int DuplicateToleranceDays { get; set; } = 5;
    public int SalaryMonthStartDay { get; set; } = 1;

    [BindProperty]
    public CustomFontInputModel CustomFontInput { get; set; } = new();

    public class CustomFontInputModel
    {
        [Required(ErrorMessage = "Le nom de la police est obligatoire.")]
        [StringLength(80, ErrorMessage = "Le nom ne doit pas dépasser 80 caractères.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "La règle CSS font-family est obligatoire.")]
        [StringLength(200, ErrorMessage = "La règle CSS ne doit pas dépasser 200 caractères.")]
        public string FontFamilyCss { get; set; } = string.Empty;

        [Url(ErrorMessage = "L'adresse URL Google Fonts est invalide.")]
        [StringLength(500, ErrorMessage = "L'URL ne doit pas dépasser 500 caractères.")]
        public string? GoogleFontUrl { get; set; }
    }

    public Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        return LoadDataAsync(cancellationToken);
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        Fonts = await uiSettingsService.GetAllFontsAsync(cancellationToken);
        ActiveFont = await uiSettingsService.GetActiveFontAsync(cancellationToken);
        ActiveFontSize = await uiSettingsService.GetFontSizeAsync(cancellationToken);
        DefaultDisplayMode = await uiSettingsService.GetDefaultDisplayModeAsync(cancellationToken);
        DefaultPeriod = await uiSettingsService.GetDefaultPeriodAsync(cancellationToken);
        TransactionsPageSize = await uiSettingsService.GetTransactionsPageSizeAsync(cancellationToken);
        ShowKpiCents = await uiSettingsService.GetShowKpiCentsAsync(cancellationToken);
        DuplicateToleranceDays = await uiSettingsService.GetDuplicateToleranceDaysAsync(cancellationToken);
        SalaryMonthStartDay = await uiSettingsService.GetSalaryMonthStartDayAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSavePreferencesAsync(Guid fontId,
        string fontSize,
        string displayMode,
        string defaultPeriod,
        int transactionsPageSize,
        bool showKpiCents,
        int duplicateToleranceDays,
        int salaryMonthStartDay = 1, CancellationToken cancellationToken = default)
    {
        await uiSettingsService.SetActiveFontAsync(fontId, cancellationToken);
        await uiSettingsService.SetFontSizeAsync(fontSize, cancellationToken);
        await uiSettingsService.SetDefaultDisplayModeAsync(displayMode, cancellationToken);
        await uiSettingsService.SetDefaultPeriodAsync(defaultPeriod, cancellationToken);
        await uiSettingsService.SetTransactionsPageSizeAsync(transactionsPageSize, cancellationToken);
        await uiSettingsService.SetShowKpiCentsAsync(showKpiCents, cancellationToken);
        await uiSettingsService.SetDuplicateToleranceDaysAsync(duplicateToleranceDays, cancellationToken);
        await uiSettingsService.SetSalaryMonthStartDayAsync(salaryMonthStartDay, cancellationToken);

        TempData["SuccessMessage"] = "Les paramètres d'affichage, de comportement et de tolérance d'import ont été enregistrés avec succès.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddCustomFontAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await LoadDataAsync(cancellationToken);
            TempData["ErrorMessage"] = "Veuillez vérifier les champs du formulaire d'ajout de police.";
            return Page();
        }

        await uiSettingsService.AddCustomFontAsync(
            CustomFontInput.Name,
            CustomFontInput.FontFamilyCss,
            CustomFontInput.GoogleFontUrl,
            cancellationToken);

        TempData["SuccessMessage"] = $"La police '{CustomFontInput.Name}' a été ajoutée avec succès en base de données.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteFontAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (success, errorMessage) = await uiSettingsService.DeleteFontAsync(id, cancellationToken);
        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage ?? "Impossible de supprimer cette police.";
        }
        else
        {
            TempData["SuccessMessage"] = "La police personnalisée a été supprimée avec succès.";
        }

        return RedirectToPage();
    }
}
