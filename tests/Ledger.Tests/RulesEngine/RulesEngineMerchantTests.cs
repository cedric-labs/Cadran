using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Merchants.Services;
using Ledger.Features.RulesEngine.Entities;
using Ledger.Features.RulesEngine.Pages;
using Ledger.Features.RulesEngine.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace Ledger.Tests.RulesEngine;

public class RulesEngineMerchantTests : IDisposable
{
    private readonly Ledger.Infrastructure.Data.AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Mock<IRulesEngineService> _mockRulesEngineService;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly Mock<IMerchantService> _mockMerchantService;
    private readonly Guid _catGroceriesId = Guid.NewGuid();

    public RulesEngineMerchantTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _mockRulesEngineService = new Mock<IRulesEngineService>();
        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();
        _mockMerchantService = new Mock<IMerchantService>();

        var catGroceries = new Category
        {
            Id = _catGroceriesId,
            UserId = "test-user-id",
            Name = "Alimentation",
            IsIncome = false
        };
        _context.Categories.Add(catGroceries);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private IndexModel CreateIndexModel()
    {
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        var pageContext = new PageContext(actionContext);
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

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
    public async Task OnPostCreateAsync_WithExistingTargetMerchantId_AssignsExistingMerchant()
    {
        // Arrange
        var existingMerchant = new Merchant
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Carrefour",
            NormalizedName = "carrefour"
        };
        _context.Merchants.Add(existingMerchant);
        await _context.SaveChangesAsync();

        var model = CreateIndexModel();
        model.Input = new IndexModel.RuleInput
        {
            Name = "Règle Carrefour",
            Pattern = "CARREFOUR.*",
            TargetCategoryId = _catGroceriesId,
            TargetMerchantId = existingMerchant.Id,
            Priority = 10
        };

        // Act
        var result = await model.OnPostCreateAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        var rule = _context.CategorizationRules.FirstOrDefault(r => r.Name == "Règle Carrefour");
        rule.Should().NotBeNull();
        rule!.TargetMerchantId.Should().Be(existingMerchant.Id);
    }

    [Fact]
    public async Task OnPostCreateAsync_WithNewMerchantName_CreatesMerchantAndAssignsToRule()
    {
        // Arrange
        var newMerchant = new Merchant
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Lidl Express",
            NormalizedName = "lidl express"
        };

        _context.Merchants.Add(newMerchant);
        await _context.SaveChangesAsync();

        _mockMerchantService
            .Setup(s => s.GetOrCreateByNameAsync("Lidl Express", null, default))
            .ReturnsAsync(newMerchant);

        var model = CreateIndexModel();
        model.Input = new IndexModel.RuleInput
        {
            Name = "Règle Lidl",
            Pattern = "LIDL.*",
            TargetCategoryId = _catGroceriesId,
            TargetMerchantId = null,
            NewMerchantName = "Lidl Express",
            Priority = 10
        };

        // Act
        var result = await model.OnPostCreateAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        _mockMerchantService.Verify(s => s.GetOrCreateByNameAsync("Lidl Express", null, default), Times.Once);

        var rule = _context.CategorizationRules.FirstOrDefault(r => r.Name == "Règle Lidl");
        rule.Should().NotBeNull();
        rule!.TargetMerchantId.Should().Be(newMerchant.Id);
    }

    [Fact]
    public async Task OnPostUpdateAsync_WithNewMerchantName_CreatesMerchantAndUpdatesRule()
    {
        // Arrange
        var rule = new CategorizationRule
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Règle Monoprix",
            Pattern = "MONOPRIX.*",
            TargetCategoryId = _catGroceriesId,
            Priority = 10,
            IsActive = true
        };
        _context.CategorizationRules.Add(rule);
        await _context.SaveChangesAsync();

        var newMerchant = new Merchant
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Monoprix Gourmet",
            NormalizedName = "monoprix gourmet"
        };

        _context.Merchants.Add(newMerchant);
        await _context.SaveChangesAsync();

        _mockMerchantService
            .Setup(s => s.GetOrCreateByNameAsync("Monoprix Gourmet", null, default))
            .ReturnsAsync(newMerchant);

        var model = CreateIndexModel();
        model.Input = new IndexModel.RuleInput
        {
            Id = rule.Id,
            Name = "Règle Monoprix Modifiée",
            Pattern = "MONOPRIX.*",
            TargetCategoryId = _catGroceriesId,
            TargetMerchantId = null,
            NewMerchantName = "Monoprix Gourmet",
            Priority = 5
        };

        // Act
        var result = await model.OnPostUpdateAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        _mockMerchantService.Verify(s => s.GetOrCreateByNameAsync("Monoprix Gourmet", null, default), Times.Once);

        var updatedRule = await _context.CategorizationRules.FindAsync(rule.Id);
        updatedRule.Should().NotBeNull();
        updatedRule!.Name.Should().Be("Règle Monoprix Modifiée");
        updatedRule.TargetMerchantId.Should().Be(newMerchant.Id);
        updatedRule.Priority.Should().Be(5);
    }

    [Fact]
    public async Task OnPostUpdateAsync_ClearingMerchant_SetsTargetMerchantIdToNull()
    {
        // Arrange
        var merchant = new Merchant
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Auchan",
            NormalizedName = "auchan"
        };
        _context.Merchants.Add(merchant);

        var rule = new CategorizationRule
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Règle Auchan",
            Pattern = "AUCHAN.*",
            TargetCategoryId = _catGroceriesId,
            TargetMerchantId = merchant.Id,
            Priority = 10,
            IsActive = true
        };
        _context.CategorizationRules.Add(rule);
        await _context.SaveChangesAsync();

        var model = CreateIndexModel();
        model.Input = new IndexModel.RuleInput
        {
            Id = rule.Id,
            Name = "Règle Auchan",
            Pattern = "AUCHAN.*",
            TargetCategoryId = _catGroceriesId,
            TargetMerchantId = null,
            NewMerchantName = null,
            Priority = 10
        };

        // Act
        var result = await model.OnPostUpdateAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        var updatedRule = await _context.CategorizationRules.FindAsync(rule.Id);
        updatedRule.Should().NotBeNull();
        updatedRule!.TargetMerchantId.Should().BeNull();
    }
}
