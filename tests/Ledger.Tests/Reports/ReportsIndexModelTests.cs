using System.Text.Json;
using FluentAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Reports.Entities;
using Ledger.Features.Reports.Models;
using Ledger.Features.Reports.Pages;
using Ledger.Features.Reports.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace Ledger.Tests.Reports;

public class ReportsIndexModelTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<IReportService> _mockReportService;
    private readonly Mock<IPivotReportService> _mockPivotService;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly ReportDashboard _defaultDashboard;

    public ReportsIndexModelTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Boursorama" };
        _context.Banks.Add(bank);

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Boursorama",
            IsArchived = false
        };
        _context.BankAccounts.Add(account);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Courses"
        };
        _context.Categories.Add(category);
        _context.SaveChanges();

        _defaultDashboard = new ReportDashboard
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Tableau Principal",
            IsDefault = true
        };

        _mockReportService = new Mock<IReportService>();
        _mockReportService.Setup(r => r.GetDashboardsAsync()).ReturnsAsync(new List<ReportDashboard> { _defaultDashboard });
        _mockReportService.Setup(r => r.GetDashboardByIdAsync(_defaultDashboard.Id)).ReturnsAsync(_defaultDashboard);
        _mockReportService.Setup(r => r.ComputeDashboardDataAsync(_defaultDashboard.Id)).ReturnsAsync(new List<WidgetDataDto>());

        _mockPivotService = new Mock<IPivotReportService>();
        _mockPivotService.Setup(p => p.GetSavedPivotReportsAsync()).ReturnsAsync(new List<SavedPivotReport>());
        _mockPivotService.Setup(p => p.ComputePivotAsync(It.IsAny<PivotQueryDto>())).ReturnsAsync(new PivotResultDto());

        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();
        _mockLocalizedEntityService
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        _mockLocalizer = new Mock<IStringLocalizer>();
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _context.Dispose();
        _connection.Dispose();
    }

    private static (PageContext pageContext, TempDataDictionary tempData) CreateContext()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        var pageContext = new PageContext(actionContext);
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        return (pageContext, tempData);
    }

    [Fact]
    public async Task OnGetAsync_DashboardsTab_ShouldLoadDashboardsAndData()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        await model.OnGetAsync(tab: "dashboards", dashboardId: _defaultDashboard.Id, savedViewId: null);

        model.ActiveTab.Should().Be("dashboards");
        model.CurrentDashboard.Should().NotBeNull();
        model.CurrentDashboard!.Id.Should().Be(_defaultDashboard.Id);
        model.Dashboards.Should().ContainSingle(d => d.Name == "Tableau Principal");
    }

    [Fact]
    public async Task OnGetAsync_PivotTab_ShouldLoadPivotData()
    {
        var savedReport = new SavedPivotReport
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Dépenses par catégorie",
            ConfigJson = "{}"
        };
        _mockPivotService.Setup(p => p.GetSavedPivotReportsAsync()).ReturnsAsync(new List<SavedPivotReport> { savedReport });
        _mockPivotService.Setup(p => p.GetSavedPivotReportByIdAsync(savedReport.Id)).ReturnsAsync(savedReport);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        await model.OnGetAsync(tab: "pivot", dashboardId: null, savedViewId: savedReport.Id);

        model.ActiveTab.Should().Be("pivot");
        model.SavedViews.Should().ContainSingle(v => v.Name == "Dépenses par catégorie");
        model.CurrentSavedView.Should().NotBeNull();
    }

    [Fact]
    public async Task OnPostCreateDashboardAsync_WhenNameEmpty_ShouldSetErrorAndRedirect()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            DashInput = new IndexModel.DashboardInput { Name = "" }
        };

        var result = await model.OnPostCreateDashboardAsync();

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues!["tab"].Should().Be("dashboards");
        model.TempData.ContainsKey("ErrorMessage").Should().BeTrue();
    }

    [Fact]
    public async Task OnPostCreateDashboardAsync_WhenValid_ShouldCreateAndRedirect()
    {
        var created = new ReportDashboard { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Budget 2026" };
        _mockReportService.Setup(r => r.CreateDashboardAsync("Budget 2026", "Description")).ReturnsAsync(created);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            DashInput = new IndexModel.DashboardInput { Name = "Budget 2026", Description = "Description" }
        };

        var result = await model.OnPostCreateDashboardAsync();

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues!["dashboardId"].Should().Be(created.Id);
        _mockReportService.Verify(r => r.CreateDashboardAsync("Budget 2026", "Description"), Times.Once);
    }

    [Fact]
    public async Task OnPostDeleteDashboardAsync_WhenCalled_DeletesDashboardAndRedirects()
    {
        var id = Guid.NewGuid();
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostDeleteDashboardAsync(id);

        result.Should().BeOfType<RedirectToPageResult>();
        _mockReportService.Verify(r => r.DeleteDashboardAsync(id), Times.Once);
    }

    [Fact]
    public async Task OnPostResetPresetsAsync_WhenCalled_ResetsPresetsAndRedirects()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostResetPresetsAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _mockReportService.Verify(r => r.ResetDefaultDashboardAsync(), Times.Once);
    }

    [Fact]
    public async Task OnPostSaveWidgetAsync_WhenTitleEmpty_ShouldSetErrorAndRedirect()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            WidgInput = new IndexModel.WidgetInput { Title = "", DashboardId = _defaultDashboard.Id }
        };

        var result = await model.OnPostSaveWidgetAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        model.TempData.ContainsKey("ErrorMessage").Should().BeTrue();
    }

    [Fact]
    public async Task OnPostSaveWidgetAsync_WhenNewWidget_ShouldCallAddWidgetAsync()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            WidgInput = new IndexModel.WidgetInput
            {
                DashboardId = _defaultDashboard.Id,
                Title = "Répartition Dépenses",
                WidgetType = "ChartDoughnutExpenses",
                WidthCols = 6,
                Period = "current_month",
                TopCount = 5
            }
        };

        var result = await model.OnPostSaveWidgetAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _mockReportService.Verify(r => r.AddWidgetAsync(
            _defaultDashboard.Id,
            "Répartition Dépenses",
            "ChartDoughnutExpenses",
            6,
            It.Is<string>(json => json.Contains("current_month"))), Times.Once);
    }

    [Fact]
    public async Task OnPostSaveWidgetAsync_WhenExistingWidget_ShouldCallUpdateWidgetAsync()
    {
        var widgetId = Guid.NewGuid();
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            WidgInput = new IndexModel.WidgetInput
            {
                Id = widgetId,
                DashboardId = _defaultDashboard.Id,
                Title = "Top Catégories",
                WidthCols = 12,
                Period = "current_year",
                TopCount = 10
            }
        };

        var result = await model.OnPostSaveWidgetAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _mockReportService.Verify(r => r.UpdateWidgetAsync(
            widgetId,
            "Top Catégories",
            12,
            It.Is<string>(json => json.Contains("current_year"))), Times.Once);
    }

    [Fact]
    public async Task OnPostDeleteWidgetAsync_WhenCalled_DeletesWidgetAndRedirects()
    {
        var widgetId = Guid.NewGuid();
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostDeleteWidgetAsync(widgetId, _defaultDashboard.Id);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues!["dashboardId"].Should().Be(_defaultDashboard.Id);
        _mockReportService.Verify(r => r.DeleteWidgetAsync(widgetId), Times.Once);
    }

    [Fact]
    public async Task OnPostSavePivotViewAsync_WhenValid_ShouldCallSavePivotReportAsync()
    {
        var savedReport = new SavedPivotReport { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Ma Vue TCD", ConfigJson = "{}" };
        _mockPivotService.Setup(p => p.SavePivotReportAsync(
            "Ma Vue TCD",
            "Vue mensuelle",
            It.IsAny<PivotQueryDto>(),
            null,
            false)).ReturnsAsync(savedReport);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            SavePivot = new IndexModel.SavePivotInput
            {
                Name = "Ma Vue TCD",
                Description = "Vue mensuelle",
                SerializedQueryJson = "{}"
            }
        };

        var result = await model.OnPostSavePivotViewAsync();

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues!["savedViewId"].Should().Be(savedReport.Id);
        _mockPivotService.Verify(p => p.SavePivotReportAsync(
            "Ma Vue TCD",
            "Vue mensuelle",
            It.IsAny<PivotQueryDto>(),
            null,
            false), Times.Once);
    }

    [Fact]
    public async Task OnPostDeletePivotViewAsync_WhenCalled_DeletesPivotViewAndRedirects()
    {
        var viewId = Guid.NewGuid();
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostDeletePivotViewAsync(viewId);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues!["tab"].Should().Be("pivot");
        _mockPivotService.Verify(p => p.DeleteSavedPivotReportAsync(viewId), Times.Once);
    }

    [Fact]
    public async Task OnGetCellTransactionsAsync_WhenCellRequested_ReturnsJsonWithLabels()
    {
        var detail = new PivotCellDetailDto
        {
            TotalAmount = 150m,
            TransactionsCount = 3
        };
        _mockPivotService.Setup(p => p.GetCellDetailAsync(It.IsAny<PivotQueryDto>(), "row-1", "col-1", default))
            .ReturnsAsync(detail);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _mockReportService.Object,
            _mockPivotService.Object,
            _context,
            _mockLocalizedEntityService.Object,
            _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnGetCellTransactionsAsync("row-1", "col-1", "Catégorie Alimentation", "Janvier 2026");

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var dto = json.Value.Should().BeOfType<PivotCellDetailDto>().Subject;
        dto.RowLabel.Should().Be("Catégorie Alimentation");
        dto.ColumnLabel.Should().Be("Janvier 2026");
        dto.TotalAmount.Should().Be(150m);
    }
}
