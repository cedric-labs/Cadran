using AwesomeAssertions;
using Ledger.Features.Imports.Entities;
using Ledger.Features.Imports.Pages;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace Ledger.Tests.Imports;

public class CsvProfilesPageModelTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly CsvProfilesModel _pageModel;
    private readonly TempDataDictionary _tempData;

    public CsvProfilesPageModelTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _mockLocalizer = new Mock<IStringLocalizer>();
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));

        var httpContext = new DefaultHttpContext();
        _tempData = new TempDataDictionary(httpContext, new Mock<ITempDataProvider>().Object);
        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new PageActionDescriptor(), new ModelStateDictionary());

        _pageModel = new CsvProfilesModel(_context, _mockLocalizer.Object)
        {
            PageContext = new PageContext(actionContext),
            TempData = _tempData
        };
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task OnGetAsync_WhenInvoked_LoadsProfilesFromDatabase()
    {
        _context.CsvMappingProfiles.Add(new CsvMappingProfile { Name = "Profile A", DateFormat = "dd/MM/yyyy", UserId = "test-user-id" });
        _context.CsvMappingProfiles.Add(new CsvMappingProfile { Name = "Profile B", DateFormat = "yyyy-MM-dd", UserId = "test-user-id" });
        await _context.SaveChangesAsync();

        var result = await _pageModel.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        _pageModel.Profiles.Should().HaveCount(2);
    }

    [Fact]
    public async Task OnPostCreateAsync_InvalidModel_ReturnsPage()
    {
        _pageModel.ModelState.AddModelError("Name", "Nom requis");

        var result = await _pageModel.OnPostCreateAsync();

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnPostCreateAsync_ValidModel_SavesAndRedirects()
    {
        _pageModel.Input = new CsvProfilesModel.ProfileInput
        {
            Name = "BoursoBank CSV",
            Delimiter = ";",
            DecimalSeparator = ",",
            DateFormat = "dd/MM/yyyy",
            HasHeader = true,
            DateColumnIndex = 0,
            LabelColumnIndex = 1,
            AmountColumnIndex = 2
        };

        var result = await _pageModel.OnPostCreateAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _tempData["SuccessMessage"].Should().NotBeNull();

        var saved = _context.CsvMappingProfiles.FirstOrDefault(p => p.Name == "BoursoBank CSV");
        saved.Should().NotBeNull();
        saved!.Delimiter.Should().Be(";");
    }

    [Fact]
    public async Task OnPostDeleteAsync_WhenInvoked_RemovesProfileAndRedirects()
    {
        var profile = new CsvMappingProfile { Name = "To Delete", DateFormat = "dd/MM/yyyy", UserId = "test-user-id" };
        _context.CsvMappingProfiles.Add(profile);
        await _context.SaveChangesAsync();

        var result = await _pageModel.OnPostDeleteAsync(profile.Id);

        result.Should().BeOfType<RedirectToPageResult>();
        _context.CsvMappingProfiles.Find(profile.Id).Should().BeNull();
    }
}
