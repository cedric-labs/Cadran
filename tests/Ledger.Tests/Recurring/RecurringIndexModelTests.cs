using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Models;
using Ledger.Features.Recurring.Pages;
using Ledger.Features.Recurring.Services;
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

namespace Ledger.Tests.Recurring;

public class RecurringIndexModelTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<IRecurringExpenseService> _mockRecurringService;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly BankAccount _account;
    private readonly Category _category;

    public RecurringIndexModelTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Société Générale" };
        _context.Banks.Add(bank);

        _account = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Courant",
            IsArchived = false
        };
        _context.BankAccounts.Add(_account);

        _category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Abonnements"
        };
        _context.Categories.Add(_category);
        _context.SaveChanges();

        _mockRecurringService = new Mock<IRecurringExpenseService>();
        _mockRecurringService
            .Setup(r => r.GetMonthlySummaryAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new MonthlyRecurringSummaryDto
            {
                TotalExpected = 45m,
                TotalDebited = 45m,
                TotalPending = 0m
            });
        _mockRecurringService
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<RecurringExpense>());
        _mockRecurringService
            .Setup(r => r.DetectCandidatesAsync())
            .ReturnsAsync(new List<RecurringCandidateDto>());

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

    private static (PageContext pageContext, TempDataDictionary tempData) CreateContext(bool isAjax = false)
    {
        var httpContext = new DefaultHttpContext();
        if (isAjax)
        {
            httpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";
        }
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        var pageContext = new PageContext(actionContext);
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        return (pageContext, tempData);
    }

    [Fact]
    public async Task OnGetAsync_WhenCalled_LoadsSummaryCategoriesAndAccounts()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(_mockRecurringService.Object, _context, _mockLocalizedEntityService.Object, _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        await model.OnGetAsync(2026, 10);

        model.SelectedYear.Should().Be(2026);
        model.SelectedMonth.Should().Be(10);
        model.Summary.Should().NotBeNull();
        model.Summary.TotalExpected.Should().Be(45m);
        model.AvailableAccounts.Should().ContainSingle(a => a.Name == "Compte Courant");
        model.AvailableCategories.Should().ContainSingle(c => c.Name == "Abonnements");
    }

    [Fact]
    public async Task OnPostSaveAsync_WithInvalidModelState_ShouldReturnPage()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(_mockRecurringService.Object, _context, _mockLocalizedEntityService.Object, _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };
        model.ModelState.AddModelError("Input.Name", "Required");

        var result = await model.OnPostSaveAsync(2026, 10);

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnPostSaveAsync_WhenCreatingNew_ShouldCallCreateAsyncAndRedirect()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(_mockRecurringService.Object, _context, _mockLocalizedEntityService.Object, _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Input = new IndexModel.RecurringExpenseInput
            {
                Name = "Netflix",
                Pattern = "NETFLIX",
                ExpectedAmount = 17.99m,
                Frequency = RecurringFrequency.Monthly,
                ExpectedDayOfMonth = 15,
                TargetBankAccountId = _account.Id,
                TargetCategoryId = _category.Id
            }
        };

        var result = await model.OnPostSaveAsync(2026, 10);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues!["year"].Should().Be(2026);
        redirect.RouteValues!["month"].Should().Be(10);

        _mockRecurringService.Verify(r => r.CreateAsync(It.Is<RecurringExpense>(e =>
            e.Name == "Netflix" && e.ExpectedAmount == 17.99m && e.Pattern == "NETFLIX")), Times.Once);
    }

    [Fact]
    public async Task OnPostSaveAsync_WhenEditingExisting_ShouldCallUpdateAsyncAndRedirect()
    {
        var expenseId = Guid.NewGuid();
        var existing = new RecurringExpense
        {
            Id = expenseId,
            UserId = "test-user-id",
            Name = "Spotify",
            Pattern = "SPOTIFY",
            ExpectedAmount = 9.99m
        };

        _mockRecurringService.Setup(r => r.GetByIdAsync(expenseId)).ReturnsAsync(existing);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(_mockRecurringService.Object, _context, _mockLocalizedEntityService.Object, _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Input = new IndexModel.RecurringExpenseInput
            {
                Id = expenseId,
                Name = "Spotify Premium Famille",
                Pattern = "SPOTIFY",
                ExpectedAmount = 14.99m
            }
        };

        var result = await model.OnPostSaveAsync(2026, 10);

        result.Should().BeOfType<RedirectToPageResult>();
        _mockRecurringService.Verify(r => r.UpdateAsync(It.Is<RecurringExpense>(e =>
            e.Id == expenseId && e.Name == "Spotify Premium Famille" && e.ExpectedAmount == 14.99m)), Times.Once);
    }

    [Fact]
    public async Task OnPostDeleteAsync_WhenCalled_CallsDeleteAsyncAndRedirects()
    {
        var expenseId = Guid.NewGuid();
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(_mockRecurringService.Object, _context, _mockLocalizedEntityService.Object, _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostDeleteAsync(expenseId, 2026, 10);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues!["year"].Should().Be(2026);
        redirect.RouteValues!["month"].Should().Be(10);
        _mockRecurringService.Verify(r => r.DeleteAsync(expenseId), Times.Once);
    }

    [Fact]
    public async Task OnPostConfirmCandidateAsync_Ajax_ShouldReturnJsonResult()
    {
        var (pageContext, tempData) = CreateContext(isAjax: true);
        var model = new IndexModel(_mockRecurringService.Object, _context, _mockLocalizedEntityService.Object, _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostConfirmCandidateAsync(
            pattern: "FREE MOBILE",
            suggestedName: "Forfait Free",
            amount: 19.99m,
            day: 5,
            frequency: (int)RecurringFrequency.Monthly,
            categoryId: _category.Id,
            bankAccountId: _account.Id,
            year: 2026,
            month: 10);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.Value.Should().NotBeNull();
        _mockRecurringService.Verify(r => r.ConfirmCandidateAsync(It.Is<RecurringCandidateDto>(c =>
            c.Pattern == "FREE MOBILE" && c.AverageAmount == 19.99m)), Times.Once);
    }

    [Fact]
    public async Task OnPostIgnoreCandidateAsync_Ajax_ShouldCallIgnoreAndReturnJson()
    {
        var (pageContext, tempData) = CreateContext(isAjax: true);
        var model = new IndexModel(_mockRecurringService.Object, _context, _mockLocalizedEntityService.Object, _mockLocalizer.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostIgnoreCandidateAsync("PRLV EDF", 2026, 10);

        result.Should().BeOfType<JsonResult>();
        _mockRecurringService.Verify(r => r.IgnoreCandidateAsync("PRLV EDF"), Times.Once);
    }
}
