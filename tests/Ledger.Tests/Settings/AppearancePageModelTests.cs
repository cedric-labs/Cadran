using FluentAssertions;
using Ledger.Features.Settings.Pages;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace Ledger.Tests.Settings;

public class AppearancePageModelTests
{
    private readonly Mock<IUiSettingsService> _mockService;
    private readonly AppearanceModel _pageModel;
    private readonly TempDataDictionary _tempData;

    public AppearancePageModelTests()
    {
        _mockService = new Mock<IUiSettingsService>();
        var httpContext = new DefaultHttpContext();
        _tempData = new TempDataDictionary(httpContext, new Mock<ITempDataProvider>().Object);

        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        _pageModel = new AppearanceModel(_mockService.Object)
        {
            PageContext = new PageContext(actionContext),
            TempData = _tempData
        };

        SetupDefaultServiceReturns();
    }

    private void SetupDefaultServiceReturns()
    {
        _mockService.Setup(s => s.GetAllFontsAsync()).ReturnsAsync(new List<UiFont>
        {
            new() { Id = Guid.NewGuid(), Name = "Inter", FontFamilyCss = "sans-serif", IsActive = true }
        });
        _mockService.Setup(s => s.GetActiveFontAsync()).ReturnsAsync(new UiFont { Id = Guid.NewGuid(), Name = "Inter", FontFamilyCss = "sans-serif", IsActive = true });
        _mockService.Setup(s => s.GetFontSizeAsync()).ReturnsAsync("14px");
        _mockService.Setup(s => s.GetDefaultDisplayModeAsync()).ReturnsAsync("compact");
        _mockService.Setup(s => s.GetDefaultPeriodAsync()).ReturnsAsync("current-month");
        _mockService.Setup(s => s.GetTransactionsPageSizeAsync()).ReturnsAsync(250);
        _mockService.Setup(s => s.GetShowKpiCentsAsync()).ReturnsAsync(true);
        _mockService.Setup(s => s.GetDuplicateToleranceDaysAsync()).ReturnsAsync(5);
        _mockService.Setup(s => s.GetSalaryMonthStartDayAsync()).ReturnsAsync(1);
    }

    [Fact]
    public async Task OnGetAsync_WhenInvoked_LoadsAllSettingsProperly()
    {
        await _pageModel.OnGetAsync();

        _pageModel.Fonts.Should().HaveCount(1);
        _pageModel.ActiveFont.Name.Should().Be("Inter");
        _pageModel.ActiveFontSize.Should().Be("14px");
        _pageModel.DefaultDisplayMode.Should().Be("compact");
        _pageModel.DefaultPeriod.Should().Be("current-month");
        _pageModel.TransactionsPageSize.Should().Be(250);
        _pageModel.ShowKpiCents.Should().BeTrue();
        _pageModel.DuplicateToleranceDays.Should().Be(5);
        _pageModel.SalaryMonthStartDay.Should().Be(1);
    }

    [Fact]
    public async Task OnPostSavePreferencesAsync_WhenInvoked_CallsServicesAndRedirects()
    {
        var fontId = Guid.NewGuid();

        var result = await _pageModel.OnPostSavePreferencesAsync(
            fontId, "16px", "comfortable", "current-year", 100, false, 7, 28);

        result.Should().BeOfType<RedirectToPageResult>();
        _tempData["SuccessMessage"].Should().NotBeNull();

        _mockService.Verify(s => s.SetActiveFontAsync(fontId), Times.Once);
        _mockService.Verify(s => s.SetFontSizeAsync("16px"), Times.Once);
        _mockService.Verify(s => s.SetDefaultDisplayModeAsync("comfortable"), Times.Once);
        _mockService.Verify(s => s.SetDefaultPeriodAsync("current-year"), Times.Once);
        _mockService.Verify(s => s.SetTransactionsPageSizeAsync(100), Times.Once);
        _mockService.Verify(s => s.SetShowKpiCentsAsync(false), Times.Once);
        _mockService.Verify(s => s.SetDuplicateToleranceDaysAsync(7), Times.Once);
        _mockService.Verify(s => s.SetSalaryMonthStartDayAsync(28), Times.Once);
    }

    [Fact]
    public async Task OnPostAddCustomFontAsync_InvalidModel_ReturnsPageWithError()
    {
        _pageModel.ModelState.AddModelError("Name", "Nom requis");

        var result = await _pageModel.OnPostAddCustomFontAsync();

        result.Should().BeOfType<PageResult>();
        _tempData["ErrorMessage"].Should().NotBeNull();
        _mockService.Verify(s => s.AddCustomFontAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task OnPostAddCustomFontAsync_ValidModel_CallsServiceAndRedirects()
    {
        _pageModel.CustomFontInput = new AppearanceModel.CustomFontInputModel
        {
            Name = "Custom Sans",
            FontFamilyCss = "'Custom Sans', sans-serif",
            GoogleFontUrl = null
        };

        var result = await _pageModel.OnPostAddCustomFontAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _tempData["SuccessMessage"].Should().NotBeNull();
        _mockService.Verify(s => s.AddCustomFontAsync("Custom Sans", "'Custom Sans', sans-serif", null), Times.Once);
    }

    [Fact]
    public async Task OnPostDeleteFontAsync_Success_SetsSuccessMessageAndRedirects()
    {
        var fontId = Guid.NewGuid();
        _mockService.Setup(s => s.DeleteFontAsync(fontId)).ReturnsAsync((true, null));

        var result = await _pageModel.OnPostDeleteFontAsync(fontId);

        result.Should().BeOfType<RedirectToPageResult>();
        _tempData["SuccessMessage"].Should().NotBeNull();
        _tempData["ErrorMessage"].Should().BeNull();
    }

    [Fact]
    public async Task OnPostDeleteFontAsync_Failure_SetsErrorMessageAndRedirects()
    {
        var fontId = Guid.NewGuid();
        _mockService.Setup(s => s.DeleteFontAsync(fontId)).ReturnsAsync((false, "Police système"));

        var result = await _pageModel.OnPostDeleteFontAsync(fontId);

        result.Should().BeOfType<RedirectToPageResult>();
        _tempData["ErrorMessage"].Should().Be("Police système");
    }
}
