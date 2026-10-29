using AwesomeAssertions;
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
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;

namespace Ledger.Tests.Accounts;

public sealed class CreateAndEditAccountModelTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<IAccountTypeService> _mockAccountTypeService;
    private readonly Mock<ICurrencyService> _mockCurrencyService;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly Bank _bank;
    private readonly CustomAccountType _customType;

    public CreateAndEditAccountModelTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        _bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "BNP Paribas" };
        _context.Banks.Add(_bank);

        _customType = new CustomAccountType
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Compte Courant",
            Code = "Checking"
        };
        _context.CustomAccountTypes.Add(_customType);
        _context.SaveChanges();

        _mockAccountTypeService = new Mock<IAccountTypeService>();
        _mockAccountTypeService
            .Setup(s => s.GetAllAccountTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomAccountType> { _customType });
        _mockAccountTypeService
            .Setup(s => s.GetByIdAsync(_customType.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_customType);

        _mockCurrencyService = new Mock<ICurrencyService>();
        _mockCurrencyService
            .Setup(s => s.GetSupportedCurrencies())
            .Returns(new List<CurrencyInfo> { new("EUR", "Euro", "€") });

        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();
        _mockLocalizedEntityService
            .Setup(s => s.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _context.Dispose();
        _connection.Dispose();
    }

    private static (PageContext pageContext, TempDataDictionary tempData, Mock<IUrlHelper> mockUrl) CreateContext(string? referer = null)
    {
        var httpContext = new DefaultHttpContext();
        if (!string.IsNullOrEmpty(referer))
        {
            httpContext.Request.Headers.Referer = referer;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        var pageContext = new PageContext(actionContext);
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        var mockUrl = new Mock<IUrlHelper>();
        mockUrl.Setup(u => u.IsLocalUrl(It.Is<string>(url => url != null && url.StartsWith('/')))).Returns(true);

        return (pageContext, tempData, mockUrl);
    }

    [Fact]
    public async Task CreateAccountModel_OnGetAsync_ShouldPopulateAvailableCollectionsAndDefaultType()
    {
        var (pageContext, tempData, _) = CreateContext();
        var model = new CreateAccountModel(_context, _mockAccountTypeService.Object, _mockCurrencyService.Object, _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        model.AvailableBanks.Should().ContainSingle(b => b.Name == "BNP Paribas");
        model.AvailableAccountTypes.Should().ContainSingle(t => t.Code == "Checking");
        model.Input.CustomAccountTypeId.Should().Be(_customType.Id);
    }

    [Fact]
    public async Task OnPostCreateAsync_WithInvalidModelState_ReturnsPage()
    {
        var (pageContext, tempData, _) = CreateContext();
        var model = new CreateAccountModel(_context, _mockAccountTypeService.Object, _mockCurrencyService.Object, _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };
        model.ModelState.AddModelError("Input.AccountName", "Required");

        var result = await model.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        model.AvailableBanks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OnPostCreateAsync_WithoutBank_AddsErrorAndReturnsPage()
    {
        var (pageContext, tempData, _) = CreateContext();
        var model = new CreateAccountModel(_context, _mockAccountTypeService.Object, _mockCurrencyService.Object, _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Input = new CreateAccountModel.InputModel
            {
                AccountName = "Livret A",
                ExistingBankId = null,
                NewBankName = null
            }
        };

        var result = await model.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        model.ModelState.ContainsKey("Input.ExistingBankId").Should().BeTrue();
    }

    [Fact]
    public async Task OnPostCreateAsync_WithExistingBank_CreatesAccountAndRedirects()
    {
        var (pageContext, tempData, _) = CreateContext();
        var model = new CreateAccountModel(_context, _mockAccountTypeService.Object, _mockCurrencyService.Object, _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Input = new CreateAccountModel.InputModel
            {
                AccountName = "Compte Courant Perso",
                ExistingBankId = _bank.Id,
                CustomAccountTypeId = _customType.Id,
                InitialBalance = 250m,
                Currency = "EUR"
            }
        };

        var result = await model.OnPostAsync();

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("/Accounts");

        var created = _context.BankAccounts.FirstOrDefault(a => a.Name == "Compte Courant Perso");
        created.Should().NotBeNull();
        created.BankId.Should().Be(_bank.Id);
        created.Type.Should().Be(AccountType.Checking);
        created.InitialBalance.Should().Be(250m);
    }

    [Fact]
    public async Task OnPostCreateAsync_WithNewBankName_CreatesBankAndAccount()
    {
        var (pageContext, tempData, _) = CreateContext();
        var model = new CreateAccountModel(_context, _mockAccountTypeService.Object, _mockCurrencyService.Object, _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Input = new CreateAccountModel.InputModel
            {
                AccountName = "Compte Pro",
                NewBankName = "Qonto",
                CustomAccountTypeId = _customType.Id,
                InitialBalance = 1500m
            }
        };

        var result = await model.OnPostAsync();

        result.Should().BeOfType<RedirectResult>();
        var createdBank = _context.Banks.FirstOrDefault(b => b.Name == "Qonto");
        createdBank.Should().NotBeNull();

        var createdAccount = _context.BankAccounts.FirstOrDefault(a => a.Name == "Compte Pro");
        createdAccount.Should().NotBeNull();
        createdAccount.BankId.Should().Be(createdBank.Id);
    }

    [Fact]
    public async Task OnGetEditAsync_WithNonExistentAccount_ReturnsNotFound()
    {
        var (pageContext, tempData, _) = CreateContext();
        var model = new EditAccountModel(_context, _mockAccountTypeService.Object, _mockCurrencyService.Object, _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnGetAsync(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OnGetEditAsync_WithValidAccount_PopulatesInput()
    {
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Compte Épargne",
            Type = AccountType.Savings,
            InitialBalance = 1000m,
            Currency = "EUR"
        };
        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync();

        var (pageContext, tempData, mockUrl) = CreateContext("https://localhost/Accounts/Details/" + account.Id);
        var model = new EditAccountModel(_context, _mockAccountTypeService.Object, _mockCurrencyService.Object, _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Url = mockUrl.Object
        };

        var result = await model.OnGetAsync(account.Id);

        result.Should().BeOfType<PageResult>();
        model.Input.AccountName.Should().Be("Compte Épargne");
        model.Input.InitialBalance.Should().Be(1000m);
        model.ReturnUrl.Should().Be("/Accounts/Details/" + account.Id);
    }

    [Fact]
    public async Task OnPostEditAsync_WithNonExistentAccount_ReturnsNotFound()
    {
        var (pageContext, tempData, _) = CreateContext();
        var model = new EditAccountModel(_context, _mockAccountTypeService.Object, _mockCurrencyService.Object, _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostAsync(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OnPostEditAsync_WithInvalidModelState_ReturnsPage()
    {
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Compte Épargne"
        };
        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync();

        var (pageContext, tempData, _) = CreateContext();
        var model = new EditAccountModel(_context, _mockAccountTypeService.Object, _mockCurrencyService.Object, _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };
        model.ModelState.AddModelError("Input.AccountName", "Required");

        var result = await model.OnPostAsync(account.Id);

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnPostEditAsync_WithValidUpdatesAndReturnUrl_UpdatesAndRedirects()
    {
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Compte Joint",
            InitialBalance = 50m
        };
        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync();

        var (pageContext, tempData, mockUrl) = CreateContext();
        var model = new EditAccountModel(_context, _mockAccountTypeService.Object, _mockCurrencyService.Object, _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Url = mockUrl.Object,
            ReturnUrl = "/Accounts",
            Input = new EditAccountModel.InputModel
            {
                AccountName = "Compte Joint Modifié",
                ExistingBankId = _bank.Id,
                CustomAccountTypeId = _customType.Id,
                InitialBalance = 75m,
                IsArchived = true
            }
        };

        var result = await model.OnPostAsync(account.Id);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("/Accounts");

        var updated = await _context.BankAccounts.FindAsync(account.Id);
        updated!.Name.Should().Be("Compte Joint Modifié");
        updated.InitialBalance.Should().Be(75m);
        updated.IsArchived.Should().BeTrue();
    }
}
