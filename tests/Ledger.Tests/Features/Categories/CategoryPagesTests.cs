using System.Text.Json;
using AwesomeAssertions;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Categories.Pages;
using Ledger.Features.Categories.Services;
using Ledger.Features.Security.Context;
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
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Ledger.Tests.Features.Categories;

public class CategoryPagesTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly Mock<ILanguageService> _mockLanguageService;
    private readonly Mock<ICategorySeedService> _mockCategorySeedService;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;

    public CategoryPagesTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();
        _mockLocalizedEntityService
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _mockLocalizedEntityService
            .Setup(l => l.GetAllTranslationsForEntityAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string>());

        _mockLanguageService = new Mock<ILanguageService>();
        _mockLanguageService
            .Setup(l => l.GetSupportedLanguages())
            .Returns(new List<LanguageInfo>
            {
                new("fr", "Français", "🇫🇷"),
                new("en", "English", "🇬🇧")
            });

        _mockCategorySeedService = new Mock<ICategorySeedService>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCurrentUserService.Setup(u => u.UserId).Returns("test-user-id");
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
    public async Task IndexModel_OnGetAsync_ShouldEnsureDefaultsAndLoadCategories()
    {
        var parentCat = new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Alimentation", ParentCategoryId = null };
        _context.Categories.Add(parentCat);
        await _context.SaveChangesAsync();

        var subCat = new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Restaurants", ParentCategoryId = parentCat.Id };
        _context.Categories.Add(subCat);
        await _context.SaveChangesAsync();

        var subSubCat = new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Fast Food", ParentCategoryId = subCat.Id };
        _context.Categories.Add(subSubCat);
        await _context.SaveChangesAsync();

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(_context, _mockLocalizedEntityService.Object, _mockCategorySeedService.Object, _mockCurrentUserService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        _mockCategorySeedService.Verify(s => s.EnsureDefaultCategoriesForUserAsync("test-user-id", It.IsAny<CancellationToken>()), Times.Once);
        model.ParentCategories.Should().ContainSingle(c => c.Name == "Alimentation");
        model.OrphanCategories.Should().ContainSingle(c => c.Name == "Fast Food");
    }

    [Fact]
    public async Task IndexModel_OnPostDeleteAsync_ShouldDetachSubCategoriesAndRemoveCategory()
    {
        var parentCat = new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Transport", ParentCategoryId = null };
        _context.Categories.Add(parentCat);
        await _context.SaveChangesAsync();

        var subCat = new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Carburant", ParentCategoryId = parentCat.Id };
        _context.Categories.Add(subCat);
        await _context.SaveChangesAsync();

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(_context, _mockLocalizedEntityService.Object, _mockCategorySeedService.Object, _mockCurrentUserService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostDeleteAsync(parentCat.Id);

        result.Should().BeOfType<RedirectToPageResult>();
        (await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == parentCat.Id)).Should().BeNull();
        var refreshedSub = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == subCat.Id);
        refreshedSub.Should().NotBeNull();
        refreshedSub!.ParentCategoryId.Should().BeNull();
    }

    [Fact]
    public async Task OnPostRestoreDefaultsAsync_WhenRestoredCountGreaterThanZero_SetsSuccessMessage()
    {
        _mockCategorySeedService
            .Setup(s => s.RestoreDefaultCategoriesAsync("test-user-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(_context, _mockLocalizedEntityService.Object, _mockCategorySeedService.Object, _mockCurrentUserService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostRestoreDefaultsAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        model.StatusMessageType.Should().Be("success");
        model.StatusMessage.Should().Contain("5");
    }

    [Fact]
    public async Task OnPostRestoreDefaultsAsync_WhenRestoredCountZero_SetsInfoMessage()
    {
        _mockCategorySeedService
            .Setup(s => s.RestoreDefaultCategoriesAsync("test-user-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(_context, _mockLocalizedEntityService.Object, _mockCategorySeedService.Object, _mockCurrentUserService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostRestoreDefaultsAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        model.StatusMessageType.Should().Be("info");
        model.StatusMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OnPostRestoreDefaultsAsync_WhenUserNotAuthenticated_ReturnsChallenge()
    {
        _mockCurrentUserService.Setup(u => u.UserId).Returns((string?)null);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(_context, _mockLocalizedEntityService.Object, _mockCategorySeedService.Object, _mockCurrentUserService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostRestoreDefaultsAsync();

        result.Should().BeOfType<ChallengeResult>();
    }

    [Fact]
    public async Task OnGetAsync_WithNonExistentId_ReturnsNotFound()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new CreateOrEditModel(_context, _mockLocalizedEntityService.Object, _mockLanguageService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnGetAsync(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OnGetAsync_WithParentId_PrePopulatesParentCategory()
    {
        var parentCat = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Logement",
            ColorHex = "#3b82f6",
            IsIncome = false
        };
        _context.Categories.Add(parentCat);
        await _context.SaveChangesAsync();

        var (pageContext, tempData) = CreateContext();
        var model = new CreateOrEditModel(_context, _mockLocalizedEntityService.Object, _mockLanguageService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnGetAsync(null, parentCat.Id);

        result.Should().BeOfType<PageResult>();
        model.Input.ParentCategoryId.Should().Be(parentCat.Id);
        model.Input.ColorHex.Should().Be("#3b82f6");
        model.Input.IsIncome.Should().BeFalse();
    }

    [Fact]
    public async Task OnGetAsync_WithExistingCategory_LoadsTranslationsAndFields()
    {
        var cat = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Loisirs",
            IconName = "bi-controller",
            ColorHex = "#10b981",
            IsIncome = false
        };
        _context.Categories.Add(cat);
        await _context.SaveChangesAsync();

        _mockLocalizedEntityService
            .Setup(l => l.GetAllTranslationsForEntityAsync("Category", cat.Id, "Name"))
            .ReturnsAsync(new Dictionary<string, string> { { "en", "Leisure" } });

        var (pageContext, tempData) = CreateContext();
        var model = new CreateOrEditModel(_context, _mockLocalizedEntityService.Object, _mockLanguageService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnGetAsync(cat.Id);

        result.Should().BeOfType<PageResult>();
        model.Input.Name.Should().Be("Loisirs");
        model.Input.IconName.Should().Be("bi-controller");
        model.ExistingTranslations.Should().ContainKey("en");
        model.ExistingTranslations.Should().ContainKey("fr");
    }

    [Fact]
    public async Task OnPostAsync_WithInvalidModelState_ReturnsPage()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new CreateOrEditModel(_context, _mockLocalizedEntityService.Object, _mockLanguageService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };
        model.ModelState.AddModelError("Input.Name", "Required");

        var result = await model.OnPostAsync(null);

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnPostAsync_WhenCreatingNewCategory_SavesCategoryAndTranslations()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new CreateOrEditModel(_context, _mockLocalizedEntityService.Object, _mockLanguageService.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Input = new CreateOrEditModel.InputModel
            {
                Name = "Santé & Bien-être",
                IconName = "bi-heart-pulse",
                ColorHex = "#ef4444",
                IsIncome = false,
                TranslationsJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    { "fr", "Santé & Bien-être" },
                    { "en", "Health & Wellness" }
                })
            }
        };

        var result = await model.OnPostAsync(null);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("/Categories");

        var created = _context.Categories.FirstOrDefault(c => c.Name == "Santé & Bien-être");
        created.Should().NotBeNull();
        created!.IconName.Should().Be("bi-heart-pulse");

        _mockLocalizedEntityService.Verify(l => l.SetTranslationAsync(
            "Category",
            created.Id,
            "Name",
            "en",
            "Health & Wellness"), Times.Once);
    }

    [Fact]
    public async Task OnPostAsync_WhenEditingExistingCategory_UpdatesAndRedirects()
    {
        var cat = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Salaire Brut",
            IconName = "bi-cash",
            ColorHex = "#22c55e",
            IsIncome = true,
            IsSalary = true
        };
        _context.Categories.Add(cat);
        await _context.SaveChangesAsync();

        var (pageContext, tempData) = CreateContext();
        var model = new CreateOrEditModel(_context, _mockLocalizedEntityService.Object, _mockLanguageService.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Input = new CreateOrEditModel.InputModel
            {
                Name = "Salaire Net",
                IconName = "bi-cash-coin",
                ColorHex = "#16a34a",
                IsIncome = true,
                IsSalary = true,
                TranslationsJson = "{}"
            }
        };

        var result = await model.OnPostAsync(cat.Id);

        result.Should().BeOfType<RedirectResult>();
        var updated = await _context.Categories.FindAsync(cat.Id);
        updated!.Name.Should().Be("Salaire Net");
        updated.IconName.Should().Be("bi-cash-coin");
        updated.IsSalary.Should().BeTrue();
    }
}
