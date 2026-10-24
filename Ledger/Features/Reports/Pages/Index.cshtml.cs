using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Reports.Entities;
using Ledger.Features.Reports.Models;
using Ledger.Features.Reports.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Ledger.Features.Reports.Pages;

[Authorize]
public class IndexModel(
    IReportService reportService,
    IPivotReportService pivotService,
    AppDbContext dbContext,
    ILocalizedEntityService localizedEntityService,
    IStringLocalizer localizer) : PageModel
{
    private readonly IReportService _reportService = reportService;
    private readonly IPivotReportService _pivotService = pivotService;
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ILocalizedEntityService _localizedEntityService = localizedEntityService;
    private readonly IStringLocalizer _localizer = localizer;

    // Gestion de l'onglet actif : "dashboards" ou "pivot"
    public string ActiveTab { get; set; } = "dashboards";

    // Dashboard state
    public List<ReportDashboard> Dashboards { get; set; } = new();
    public ReportDashboard? CurrentDashboard { get; set; }
    public List<WidgetDataDto> WidgetDataList { get; set; } = new();
    public List<Category> AvailableCategories { get; set; } = new();
    public List<BankAccount> AvailableAccounts { get; set; } = new();
    public Dictionary<Guid, string> CategoryTranslations { get; set; } = new();
    public Guid? SelectedDashboardId { get; set; }

    [BindProperty]
    public DashboardInput DashInput { get; set; } = new();

    [BindProperty]
    public WidgetInput WidgInput { get; set; } = new();

    // Pivot Table (TCD) state
    [BindProperty(SupportsGet = true)]
    public PivotQueryDto PivotQuery { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid? SavedViewId { get; set; }

    public PivotResultDto? PivotResult { get; set; }
    public List<SavedPivotReport> SavedViews { get; set; } = new();
    public SavedPivotReport? CurrentSavedView { get; set; }

    [BindProperty]
    public SavePivotInput SavePivot { get; set; } = new();

    public class DashboardInput
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Common.Required")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    public class WidgetInput
    {
        public Guid? Id { get; set; }
        public Guid DashboardId { get; set; }

        [Required(ErrorMessage = "Common.Required")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Common.Required")]
        public string WidgetType { get; set; } = "ChartDoughnutExpenses";

        public int WidthCols { get; set; } = 6;
        public string Period { get; set; } = "current_month";
        public int TopCount { get; set; } = 5;
    }

    public class SavePivotInput
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Common.Required")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public bool IsDefault { get; set; } = false;
        public string SerializedQueryJson { get; set; } = "{}";
    }

    public async Task OnGetAsync(string? tab, Guid? dashboardId, Guid? savedViewId, CancellationToken cancellationToken = default)
    {
        ActiveTab = string.Equals(tab, "pivot", StringComparison.OrdinalIgnoreCase) ? "pivot" : "dashboards";
        SelectedDashboardId = dashboardId;
        SavedViewId = savedViewId;

        await LoadDataAsync(cancellationToken);

        if (ActiveTab == "pivot")
        {
            await LoadPivotDataAsync(cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostCreateDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(DashInput.Name))
        {
            TempData["ErrorMessage"] = _localizer["Reports.DashboardNameRequired"].Value;
            return RedirectToPage(new { tab = "dashboards" });
        }

        var created = await _reportService.CreateDashboardAsync(DashInput.Name, DashInput.Description, cancellationToken);
        TempData["SuccessMessage"] = _localizer["Reports.DashboardCreatedSuccess"].Value;
        return RedirectToPage(new { tab = "dashboards", dashboardId = created.Id });
    }

    public async Task<IActionResult> OnPostDeleteDashboardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _reportService.DeleteDashboardAsync(id, cancellationToken);
        TempData["SuccessMessage"] = _localizer["Reports.DashboardDeletedSuccess"].Value;
        return RedirectToPage(new { tab = "dashboards" });
    }

    public async Task<IActionResult> OnPostResetPresetsAsync(CancellationToken cancellationToken = default)
    {
        await _reportService.ResetDefaultDashboardAsync(cancellationToken);
        TempData["SuccessMessage"] = _localizer["Reports.PresetsResetSuccess"].Value;
        return RedirectToPage(new { tab = "dashboards" });
    }

    public async Task<IActionResult> OnPostSaveWidgetAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(WidgInput.Title))
        {
            TempData["ErrorMessage"] = _localizer["Reports.WidgetTitleRequired"].Value;
            return RedirectToPage(new { tab = "dashboards", dashboardId = WidgInput.DashboardId });
        }

        var config = new WidgetConfigDto
        {
            Period = WidgInput.Period,
            TopCount = WidgInput.TopCount > 0 ? WidgInput.TopCount : 5
        };
        var configJson = JsonSerializer.Serialize(config);

        if (WidgInput.Id.HasValue && WidgInput.Id.Value != Guid.Empty)
        {
            await _reportService.UpdateWidgetAsync(WidgInput.Id.Value, WidgInput.Title, WidgInput.WidthCols, configJson, cancellationToken);
            TempData["SuccessMessage"] = _localizer["Reports.WidgetUpdatedSuccess"].Value;
        }
        else
        {
            await _reportService.AddWidgetAsync(WidgInput.DashboardId, WidgInput.Title, WidgInput.WidgetType, WidgInput.WidthCols, configJson, cancellationToken);
            TempData["SuccessMessage"] = _localizer["Reports.WidgetAddedSuccess"].Value;
        }

        return RedirectToPage(new { tab = "dashboards", dashboardId = WidgInput.DashboardId });
    }

    public async Task<IActionResult> OnPostDeleteWidgetAsync(Guid widgetId, Guid dashboardId, CancellationToken cancellationToken = default)
    {
        await _reportService.DeleteWidgetAsync(widgetId, cancellationToken);
        TempData["SuccessMessage"] = _localizer["Reports.WidgetDeletedSuccess"].Value;
        return RedirectToPage(new { tab = "dashboards", dashboardId });
    }

    // =========================================================================
    // HANDLERS TCD (PIVOT TABLE)
    // =========================================================================

    public async Task<IActionResult> OnPostSavePivotViewAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SavePivot.Name))
        {
            TempData["ErrorMessage"] = "Le nom de la vue est requis.";
            return RedirectToPage(new { tab = "pivot" });
        }

        var queryToSave = TryDeserializePivotQuery(SavePivot.SerializedQueryJson, PivotQuery);

        var saved = await _pivotService.SavePivotReportAsync(
            SavePivot.Name,
            SavePivot.Description,
            queryToSave,
            SavePivot.Id,
            SavePivot.IsDefault,
            cancellationToken);

        TempData["SuccessMessage"] = _localizer["Reports.PivotViewSavedSuccess"].Value;
        return RedirectToPage(new { tab = "pivot", savedViewId = saved.Id });
    }

    public async Task<IActionResult> OnPostDeletePivotViewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _pivotService.DeleteSavedPivotReportAsync(id, cancellationToken);
        TempData["SuccessMessage"] = _localizer["Reports.PivotViewDeletedSuccess"].Value;
        return RedirectToPage(new { tab = "pivot" });
    }

    public async Task<IActionResult> OnGetCellTransactionsAsync(string rowKey, string colKey, string? rowLabel, string? colLabel, CancellationToken cancellationToken = default)
    {
        var detail = await _pivotService.GetCellDetailAsync(PivotQuery, rowKey, colKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(rowLabel)) detail.RowLabel = rowLabel;
        if (!string.IsNullOrWhiteSpace(colLabel)) detail.ColumnLabel = colLabel;
        return new JsonResult(detail);
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        Dashboards = await _reportService.GetDashboardsAsync(cancellationToken);

        if (SelectedDashboardId.HasValue)
        {
            CurrentDashboard = Dashboards.FirstOrDefault(d => d.Id == SelectedDashboardId.Value);
        }

        if (CurrentDashboard == null)
        {
            CurrentDashboard = Dashboards.FirstOrDefault(d => d.IsDefault) ?? Dashboards.FirstOrDefault();
        }

        if (CurrentDashboard != null)
        {
            SelectedDashboardId = CurrentDashboard.Id;
            if (ActiveTab == "dashboards")
            {
                WidgetDataList = await _reportService.ComputeDashboardDataAsync(CurrentDashboard.Id, cancellationToken);
            }
        }

        AvailableCategories = await _dbContext.Categories
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var catIds = AvailableCategories.Select(c => c.Id).ToList();
        CategoryTranslations = await _localizedEntityService.GetTranslationsAsync("Category", catIds, "Name", cancellationToken: cancellationToken);

        AvailableAccounts = await _dbContext.BankAccounts
            .Where(a => !a.IsArchived)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    private async Task LoadPivotDataAsync(CancellationToken cancellationToken = default)
    {
        SavedViews = await _pivotService.GetSavedPivotReportsAsync(cancellationToken);

        bool hasExplicitFilterParams = Request.Query.ContainsKey("FlowFilter")
            || Request.Query.ContainsKey("RowDimension")
            || Request.Query.ContainsKey("ColumnDimension")
            || Request.Query.ContainsKey("MetricType")
            || Request.Query.ContainsKey("PeriodPreset")
            || Request.Query.ContainsKey("SearchTerm")
            || Request.Query.ContainsKey("StartDate")
            || Request.Query.ContainsKey("EndDate");

        if (SavedViewId.HasValue)
        {
            CurrentSavedView = SavedViews.FirstOrDefault(s => s.Id == SavedViewId.Value);
            // Si l'utilisateur n'a pas soumis de changement manuel de filtre via le formulaire,
            // on charge la configuration sauvegardée. S'il a cliqué sur un sélecteur (submit),
            // ses valeurs liées (Model.PivotQuery) sont conservées et actives !
            if (CurrentSavedView != null && !hasExplicitFilterParams)
            {
                PivotQuery = TryDeserializePivotQuery(CurrentSavedView.ConfigJson, PivotQuery);
            }
        }
        else if (!hasExplicitFilterParams)
        {
            // Vérifier si une vue par défaut existe
            var defaultView = SavedViews.FirstOrDefault(s => s.IsDefault);
            if (defaultView != null)
            {
                CurrentSavedView = defaultView;
                SavedViewId = defaultView.Id;
                PivotQuery = TryDeserializePivotQuery(defaultView.ConfigJson, PivotQuery);
            }
        }

        PivotResult = await _pivotService.ComputePivotAsync(PivotQuery, cancellationToken);
    }

    private static PivotQueryDto TryDeserializePivotQuery(string? json, PivotQueryDto fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<PivotQueryDto>(json) ?? fallback;
        }
        catch (JsonException)
        {
            // Repli gracieux : si la configuration de la vue croisée est invalide, retourner la configuration par défaut
            return fallback;
        }
    }
}
