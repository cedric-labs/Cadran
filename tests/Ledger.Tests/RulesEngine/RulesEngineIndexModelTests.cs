using AwesomeAssertions;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Merchants.Services;
using Ledger.Features.RulesEngine.Entities;
using Ledger.Features.RulesEngine.Pages;
using Ledger.Features.RulesEngine.Services;
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
using Moq;
using Xunit;

namespace Ledger.Tests.RulesEngine;

public class RulesEngineIndexModelTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<IRulesEngineService> _mockRulesEngineService;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly Mock<IMerchantService> _mockMerchantService;

    public RulesEngineIndexModelTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _mockRulesEngineService = new Mock<IRulesEngineService>();
        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();
        _mockMerchantService = new Mock<IMerchantService>();

        _mockLocalizedEntityService
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        _mockMerchantService
            .Setup(m => m.GetAllMerchantsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Merchant>());
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

    private IndexModel CreateModel()
    {
        var (pageContext, tempData) = CreateContext();
        return new IndexModel(
            _context,
            _mockRulesEngineService.Object,
            _mockLocalizedEntityService.Object,
            _mockMerchantService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };
    }

    [Fact]
    public async Task OnPostUpdateAsync_WhenValid_ShouldUpdateRuleAndRedirectWithFragment()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Courses", UserId = "user-1" };
        var rule = new CategorizationRule
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Name = "Old Rule Name",
            Pattern = "AUCHAN*",
            TargetCategoryId = category.Id,
            Priority = 10,
            IsActive = true
        };
        _context.Categories.Add(category);
        _context.CategorizationRules.Add(rule);
        await _context.SaveChangesAsync();

        var model = CreateModel();
        model.Input = new IndexModel.RuleInput
        {
            Id = rule.Id,
            Name = "New Rule Name",
            Pattern = "AUCHAN DRIVE*",
            TargetCategoryId = category.Id,
            Priority = 5,
            ShiftToNextMonth = true
        };

        var result = await model.OnPostUpdateAsync();

        var redirectResult = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirectResult.Fragment.Should().Be($"rule-{rule.Id}");
        model.TempData["UpdatedRuleId"]!.ToString().Should().Be(rule.Id.ToString());
        model.TempData["SuccessMessage"]!.ToString().Should().Contain("New Rule Name");

        var updatedRule = await _context.CategorizationRules.FindAsync(rule.Id);
        updatedRule.Should().NotBeNull();
        updatedRule!.Name.Should().Be("New Rule Name");
        updatedRule.Pattern.Should().Be("AUCHAN DRIVE*");
        updatedRule.Priority.Should().Be(5);
        updatedRule.ShiftToNextMonth.Should().BeTrue();
    }

    [Fact]
    public async Task OnPostUpdateAsync_WhenRuleNotFound_ShouldSetErrorMessageAndRedirect()
    {
        var model = CreateModel();
        model.Input = new IndexModel.RuleInput
        {
            Id = Guid.NewGuid(),
            Name = "Ghost Rule",
            Pattern = "TEST*",
            TargetCategoryId = Guid.NewGuid()
        };

        var result = await model.OnPostUpdateAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        model.TempData["ErrorMessage"]!.ToString().Should().Contain("introuvable");
    }

    [Fact]
    public async Task OnPostUpdateAsync_WhenModelStateInvalid_ShouldReturnPageResult()
    {
        var model = CreateModel();
        model.Input = new IndexModel.RuleInput
        {
            Id = Guid.NewGuid(),
            Pattern = "[Invalid Regex"
        };
        model.ModelState.AddModelError("Input.Name", "Name is required");

        var result = await model.OnPostUpdateAsync();

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnPostCreateAsync_WhenValid_ShouldCreateRuleAndRedirectWithFragment()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Transports", UserId = "user-1" };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var model = CreateModel();
        model.Input = new IndexModel.RuleInput
        {
            Name = "SNCF Rule",
            Pattern = "SNCF*",
            TargetCategoryId = category.Id,
            Priority = 2,
            ShiftToNextMonth = false
        };

        var result = await model.OnPostCreateAsync();

        var redirectResult = result.Should().BeOfType<RedirectToPageResult>().Subject;
        var createdRule = _context.CategorizationRules.FirstOrDefault(r => r.Name == "SNCF Rule");
        createdRule.Should().NotBeNull();
        redirectResult.Fragment.Should().Be($"rule-{createdRule!.Id}");
        model.TempData["UpdatedRuleId"]!.ToString().Should().Be(createdRule.Id.ToString());
        model.TempData["SuccessMessage"]!.ToString().Should().Contain("SNCF Rule");
    }

    [Fact]
    public async Task OnPostCreateAsync_WhenNewMerchantProvided_ShouldCreateMerchant()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Transports", UserId = "user-1" };
        var newMerchant = new Merchant { Id = Guid.NewGuid(), UserId = "user-1", Name = "TotalEnergies", NormalizedName = "TOTALENERGIES" };
        _context.Categories.Add(category);
        _context.Merchants.Add(newMerchant);
        await _context.SaveChangesAsync();

        _mockMerchantService
            .Setup(m => m.GetOrCreateByNameAsync("TotalEnergies", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newMerchant);

        var model = CreateModel();
        model.Input = new IndexModel.RuleInput
        {
            Name = "Carburant Total",
            Pattern = "TOTAL*",
            TargetCategoryId = category.Id,
            NewMerchantName = "TotalEnergies",
            Priority = 1
        };

        var result = await model.OnPostCreateAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        var createdRule = _context.CategorizationRules.FirstOrDefault(r => r.Name == "Carburant Total");
        createdRule.Should().NotBeNull();
        createdRule!.TargetMerchantId.Should().Be(newMerchant.Id);
    }

    [Fact]
    public async Task OnPostDeleteAsync_WhenCalled_DeletesRuleAndRedirects()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Transports", UserId = "user-1" };
        _context.Categories.Add(category);
        var rule = new CategorizationRule
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Name = "To Delete",
            Pattern = "DELETE*",
            TargetCategoryId = category.Id
        };
        _context.CategorizationRules.Add(rule);
        await _context.SaveChangesAsync();

        var model = CreateModel();
        var result = await model.OnPostDeleteAsync(rule.Id);

        result.Should().BeOfType<RedirectToPageResult>();
        (await _context.CategorizationRules.FindAsync(rule.Id)).Should().BeNull();
        model.TempData["SuccessMessage"]!.ToString().Should().Contain("supprimée");
    }

    [Fact]
    public async Task OnPostApplyRulesAsync_WhenCountGreaterThanZero_ShouldSetSuccessMessage()
    {
        _mockRulesEngineService
            .Setup(s => s.ApplyRulesToDatabaseAsync(true, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var model = CreateModel();
        var result = await model.OnPostApplyRulesAsync(overwriteExisting: true);

        result.Should().BeOfType<RedirectToPageResult>();
        model.TempData["SuccessMessage"]!.ToString().Should().Contain("5 opération(s)");
    }

    [Fact]
    public async Task OnPostApplyRulesAsync_WhenCountZero_ShouldSetInfoMessage()
    {
        _mockRulesEngineService
            .Setup(s => s.ApplyRulesToDatabaseAsync(false, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var model = CreateModel();
        var result = await model.OnPostApplyRulesAsync(overwriteExisting: false);

        result.Should().BeOfType<RedirectToPageResult>();
        model.TempData["InfoMessage"]!.ToString().Should().Contain("Aucune opération");
    }
}
