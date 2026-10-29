using AwesomeAssertions;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Merchants.Pages;
using Ledger.Features.Merchants.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;

namespace Ledger.Tests.Merchants;

public sealed class MerchantPageModelTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<IMerchantService> _mockMerchantService;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly IndexModel _pageModel;
    private readonly TempDataDictionary _tempData;

    public MerchantPageModelTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _mockMerchantService = new Mock<IMerchantService>();
        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();

        _mockMerchantService
            .Setup(s => s.GetAllMerchantsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Merchant>());
        _mockMerchantService
            .Setup(s => s.GetTransactionCountsByMerchantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());

        _mockLocalizedEntityService
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var httpContext = new DefaultHttpContext();
        _tempData = new TempDataDictionary(httpContext, new Mock<ITempDataProvider>().Object);
        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new PageActionDescriptor(), new ModelStateDictionary());

        _pageModel = new IndexModel(_mockMerchantService.Object, _context, _mockLocalizedEntityService.Object)
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
    public async Task OnGetAsync_WhenSearchTermProvided_LoadsMerchantsAndFiltersBySearchTerm()
    {
        var cat = new Category { Id = Guid.NewGuid(), Name = "Supermarché", ColorHex = "#fff", UserId = "u1" };
        var m1 = new Merchant { Id = Guid.NewGuid(), Name = "Carrefour", NormalizedName = "carrefour", UserId = "u1", DefaultCategory = cat };
        var m2 = new Merchant { Id = Guid.NewGuid(), Name = "Leroy Merlin", NormalizedName = "leroy merlin", UserId = "u1" };

        _mockMerchantService.Setup(s => s.GetAllMerchantsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Merchant> { m1, m2 });
        _mockMerchantService.Setup(s => s.GetTransactionCountsByMerchantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { { m1.Id, 5 }, { m2.Id, 2 } });

        _pageModel.SearchTerm = "Carrefour";

        await _pageModel.OnGetAsync();

        _pageModel.Merchants.Should().HaveCount(1);
        _pageModel.Merchants[0].Merchant.Name.Should().Be("Carrefour");
        _pageModel.Merchants[0].TransactionCount.Should().Be(5);
    }

    [Fact]
    public async Task OnPostSaveAsync_InvalidModel_ReturnsPage()
    {
        _pageModel.ModelState.AddModelError("Name", "Requis");

        var result = await _pageModel.OnPostSaveAsync();

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnPostSaveAsync_NewMerchant_CreatesAndRedirects()
    {
        var newMerchant = new Merchant { Id = Guid.NewGuid(), Name = "Fnac", NormalizedName = "fnac", UserId = "u1" };
        _mockMerchantService.Setup(s => s.GetOrCreateByNameAsync("Fnac", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newMerchant);

        _pageModel.Input = new IndexModel.MerchantInputModel
        {
            Name = "Fnac",
            ColorHex = "#ff0000",
            IconName = "bi-shop"
        };

        var result = await _pageModel.OnPostSaveAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _tempData["SuccessMessage"].Should().NotBeNull();
        _mockMerchantService.Verify(s => s.UpdateAsync(newMerchant.Id, "Fnac", null, "bi-shop", "#ff0000", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnPostSaveAsync_ExistingMerchant_UpdatesAndRedirects()
    {
        var id = Guid.NewGuid();
        _pageModel.Input = new IndexModel.MerchantInputModel
        {
            Id = id,
            Name = "Decathlon",
            DefaultCategoryId = null,
            IconName = "bi-bag",
            ColorHex = "#0000ff"
        };

        var result = await _pageModel.OnPostSaveAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _mockMerchantService.Verify(s => s.UpdateAsync(id, "Decathlon", null, "bi-bag", "#0000ff", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnPostDeleteAsync_Existing_DeletesAndRedirects()
    {
        var id = Guid.NewGuid();
        _mockMerchantService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Merchant { Id = id, Name = "Boulanger", NormalizedName = "boulanger", UserId = "u1" });

        var result = await _pageModel.OnPostDeleteAsync(id);

        result.Should().BeOfType<RedirectToPageResult>();
        _tempData["SuccessMessage"].Should().NotBeNull();
        _mockMerchantService.Verify(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnPostDeleteAsync_NonExisting_ReturnsNotFound()
    {
        _mockMerchantService.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Merchant?)null);

        var result = await _pageModel.OnPostDeleteAsync(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OnPostMergeAsync_SameId_ReturnsRedirectWithError()
    {
        var id = Guid.NewGuid();

        var result = await _pageModel.OnPostMergeAsync(id, id);

        result.Should().BeOfType<RedirectToPageResult>();
        _tempData["ErrorMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task OnPostMergeAsync_ValidMerchants_MergesAndRedirects()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        _mockMerchantService.Setup(s => s.GetByIdAsync(id1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Merchant { Id = id1, Name = "Amazon Fr", NormalizedName = "amazon fr", UserId = "u1" });
        _mockMerchantService.Setup(s => s.GetByIdAsync(id2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Merchant { Id = id2, Name = "Amazon", NormalizedName = "amazon", UserId = "u1" });
        _mockMerchantService.Setup(s => s.MergeMerchantsAsync(id1, id2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);

        var result = await _pageModel.OnPostMergeAsync(id1, id2);

        result.Should().BeOfType<RedirectToPageResult>();
        _tempData["SuccessMessage"]?.ToString().Should().Contain("12");
    }

    [Fact]
    public async Task OnPostMergeAsync_NullSourceOrTarget_ReturnsNotFound()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        _mockMerchantService.Setup(s => s.GetByIdAsync(id1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Merchant?)null);
        _mockMerchantService.Setup(s => s.GetByIdAsync(id2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Merchant { Id = id2, Name = "Target", NormalizedName = "target", UserId = "u1" });

        var resSourceNull = await _pageModel.OnPostMergeAsync(id1, id2);
        resSourceNull.Should().BeOfType<NotFoundResult>();

        _mockMerchantService.Setup(s => s.GetByIdAsync(id1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Merchant { Id = id1, Name = "Source", NormalizedName = "source", UserId = "u1" });
        _mockMerchantService.Setup(s => s.GetByIdAsync(id2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Merchant?)null);

        var resTargetNull = await _pageModel.OnPostMergeAsync(id1, id2);
        resTargetNull.Should().BeOfType<NotFoundResult>();
    }
}
