using FluentAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Pages;
using Ledger.Features.Accounts.Services;
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

namespace Ledger.Tests.Accounts;

public class AccountCustomTypesAndStatusesPageModelTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<IAccountTypeService> _mockTypeService;
    private readonly Mock<ITransactionStatusService> _mockStatusService;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly Mock<IAccountBalanceService> _mockBalanceService;
    private readonly TempDataDictionary _tempData;

    public AccountCustomTypesAndStatusesPageModelTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _mockTypeService = new Mock<IAccountTypeService>();
        _mockStatusService = new Mock<ITransactionStatusService>();
        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();
        _mockBalanceService = new Mock<IAccountBalanceService>();

        _mockTypeService
            .Setup(s => s.GetAllAccountTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomAccountType>());
        _mockStatusService
            .Setup(s => s.GetAllStatusesAsync())
            .ReturnsAsync(new List<TransactionCustomStatus>());

        _mockLocalizedEntityService
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var httpContext = new DefaultHttpContext();
        _tempData = new TempDataDictionary(httpContext, new Mock<ITempDataProvider>().Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private static PageContext CreatePageContext()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        return new PageContext(actionContext);
    }

    #region TypesModel Tests

    [Fact]
    public async Task TypesModel_OnGetAsync_LoadsTypesAndTranslations()
    {
        var type1 = new CustomAccountType { Id = Guid.NewGuid(), Name = "Livret A", CategoryGroup = AccountCategoryGroup.Savings, ColorHex = "#000", IconName = "bi-bank", UserId = "u1" };
        _mockTypeService.Setup(s => s.GetAllAccountTypesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<CustomAccountType> { type1 });

        var model = new TypesModel(_mockTypeService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData
        };

        await model.OnGetAsync();

        model.AccountTypes.Should().HaveCount(1);
        model.AccountTypes[0].Type.Name.Should().Be("Livret A");
    }

    [Fact]
    public async Task OnPostSaveTypeAsync_WhenModelIsInvalid_ReturnsPage()
    {
        var model = new TypesModel(_mockTypeService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData
        };
        model.ModelState.AddModelError("Name", "Nom requis");

        var result = await model.OnPostSaveAsync();

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnPostSaveTypeAsync_WhenNewType_CreatesAndRedirects()
    {
        var model = new TypesModel(_mockTypeService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData,
            Input = new TypesModel.TypeInputModel
            {
                Name = "Nouveau Compte",
                NameEn = "New Account",
                ColorHex = "#123456",
                IconName = "bi-wallet"
            }
        };

        var result = await model.OnPostSaveAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _mockTypeService.Verify(s => s.CreateAsync(It.Is<CustomAccountType>(t => t.Name == "Nouveau Compte"), It.IsAny<CancellationToken>()), Times.Once);
        _mockLocalizedEntityService.Verify(s => s.SetTranslationAsync("CustomAccountType", It.IsAny<Guid>(), "Name", "en", "New Account"), Times.Once);
    }

    [Fact]
    public async Task OnPostSaveTypeAsync_WhenExistingType_UpdatesAndRedirects()
    {
        var id = Guid.NewGuid();
        var existing = new CustomAccountType { Id = id, Name = "Old Name", ColorHex = "#111", IconName = "bi-piggy", UserId = "u1" };
        _mockTypeService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var model = new TypesModel(_mockTypeService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData,
            Input = new TypesModel.TypeInputModel
            {
                Id = id,
                Name = "Updated Name",
                ColorHex = "#222",
                IconName = "bi-piggy-bank"
            }
        };

        var result = await model.OnPostSaveAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        existing.Name.Should().Be("Updated Name");
        _mockTypeService.Verify(s => s.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnPostDeleteTypeAsync_WhenInvoked_HandlesSuccessAndFailure()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _mockTypeService.Setup(s => s.DeleteAsync(id1, It.IsAny<CancellationToken>())).ReturnsAsync((true, null));
        _mockTypeService.Setup(s => s.DeleteAsync(id2, It.IsAny<CancellationToken>())).ReturnsAsync((false, "Type système non supprimable"));

        var model = new TypesModel(_mockTypeService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData
        };

        var resSuccess = await model.OnPostDeleteAsync(id1);
        resSuccess.Should().BeOfType<RedirectToPageResult>();
        _tempData["SuccessMessage"].Should().NotBeNull();

        var resFail = await model.OnPostDeleteAsync(id2);
        resFail.Should().BeOfType<RedirectToPageResult>();
        _tempData["ErrorMessage"].Should().Be("Type système non supprimable");
    }

    [Fact]
    public async Task OnPostSaveTypeAsync_WhenExistingTypeNotFound_ReturnsRedirect()
    {
        var id = Guid.NewGuid();
        _mockTypeService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((CustomAccountType?)null);

        var model = new TypesModel(_mockTypeService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData,
            Input = new TypesModel.TypeInputModel { Id = id, Name = "Perdu" }
        };

        var result = await model.OnPostSaveAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _tempData["ErrorMessage"].Should().NotBeNull();
    }

    #endregion

    #region StatusesModel Tests

    [Fact]
    public async Task StatusesModel_OnGetAsync_LoadsStatuses()
    {
        var s1 = new TransactionCustomStatus { Id = Guid.NewGuid(), Name = "En attente", ColorHex = "#ff0", IconName = "bi-clock", UserId = "u1" };
        _mockStatusService.Setup(s => s.GetAllStatusesAsync()).ReturnsAsync(new List<TransactionCustomStatus> { s1 });

        var model = new StatusesModel(_mockStatusService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData
        };

        await model.OnGetAsync();

        model.Statuses.Should().HaveCount(1);
        model.Statuses[0].Status.Name.Should().Be("En attente");
    }

    [Fact]
    public async Task OnPostSaveStatusAsync_WhenNewStatus_CreatesAndRedirects()
    {
        var model = new StatusesModel(_mockStatusService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData,
            Input = new StatusesModel.StatusInputModel
            {
                Name = "Validé",
                NameEn = "Validated",
                ColorHex = "#00ff00",
                IconName = "bi-check"
            }
        };

        var result = await model.OnPostSaveAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _mockStatusService.Verify(s => s.CreateAsync(It.Is<TransactionCustomStatus>(s => s.Name == "Validé")), Times.Once);
    }

    [Fact]
    public async Task OnPostDeleteStatusAsync_WhenInvoked_HandlesSuccessAndFailure()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _mockStatusService.Setup(s => s.DeleteAsync(id1)).ReturnsAsync((true, null));
        _mockStatusService.Setup(s => s.DeleteAsync(id2)).ReturnsAsync((false, "Statut rattaché à des opérations"));

        var model = new StatusesModel(_mockStatusService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData
        };

        var resSuccess = await model.OnPostDeleteAsync(id1);
        resSuccess.Should().BeOfType<RedirectToPageResult>();

        var resFail = await model.OnPostDeleteAsync(id2);
        resFail.Should().BeOfType<RedirectToPageResult>();
        _tempData["ErrorMessage"].Should().Be("Statut rattaché à des opérations");
    }

    [Fact]
    public async Task OnPostSaveStatusAsync_WhenExistingStatus_UpdatesAndRedirects()
    {
        var id = Guid.NewGuid();
        var existing = new TransactionCustomStatus { Id = id, UserId = "u1", Name = "Initial" };
        _mockStatusService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(existing);

        var model = new StatusesModel(_mockStatusService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData,
            Input = new StatusesModel.StatusInputModel
            {
                Id = id,
                Name = "Mis à jour",
                NameEn = "Updated",
                ColorHex = "#654321",
                IconName = "bi-star",
                IsDefault = true
            }
        };

        var result = await model.OnPostSaveAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        existing.Name.Should().Be("Mis à jour");
        _mockStatusService.Verify(s => s.UpdateAsync(existing), Times.Once);
        _mockLocalizedEntityService.Verify(s => s.SetTranslationAsync("TransactionCustomStatus", id, "Name", "en", "Updated"), Times.Once);
    }

    [Fact]
    public async Task OnPostSaveStatusAsync_WhenStatusNotFound_ReturnsRedirect()
    {
        var id = Guid.NewGuid();
        _mockStatusService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((TransactionCustomStatus?)null);

        var model = new StatusesModel(_mockStatusService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData,
            Input = new StatusesModel.StatusInputModel { Id = id, Name = "Perdu" }
        };

        var result = await model.OnPostSaveAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _tempData["ErrorMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task OnPostSetDefaultStatusAsync_WhenInvoked_HandlesSuccessAndNotFound()
    {
        var id = Guid.NewGuid();
        var status = new TransactionCustomStatus { Id = id, UserId = "u1", Name = "Mon Statut", IsDefault = false };
        _mockStatusService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(status);

        var model = new StatusesModel(_mockStatusService.Object, _context, _mockLocalizedEntityService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = _tempData
        };

        var resSuccess = await model.OnPostSetDefaultAsync(id);
        resSuccess.Should().BeOfType<RedirectToPageResult>();
        status.IsDefault.Should().BeTrue();
        _mockStatusService.Verify(s => s.UpdateAsync(status), Times.Once);

        var resNotFound = await model.OnPostSetDefaultAsync(Guid.NewGuid());
        resNotFound.Should().BeOfType<RedirectToPageResult>();
        _tempData["ErrorMessage"].Should().NotBeNull();
    }

    #endregion

    #region Accounts IndexModel Tests

    [Fact]
    public async Task AccountsIndexModel_OnGetAsync_LoadsDashboard()
    {
        var summary = new DashboardSummaryDto(1500m, 3000m, 1500m, 0.5m, new List<AccountBalanceSummaryDto>());
        _mockBalanceService.Setup(s => s.GetDashboardSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(summary);

        var model = new Ledger.Features.Accounts.Pages.IndexModel(_mockBalanceService.Object);

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        model.Dashboard.TotalNetWorth.Should().Be(1500m);
    }

    #endregion
}
