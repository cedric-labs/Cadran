using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Pages;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Merchants.Services;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Services;
using Ledger.Features.RulesEngine.Services;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Ledger.Tests.Accounts;

public sealed class TransactionsPageFilteringTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<ITransactionStatusService> _mockStatusService;
    private readonly Mock<IUiSettingsService> _mockUiSettings;
    private readonly Mock<IRulesEngineService> _mockRulesEngine;
    private readonly Mock<IMerchantService> _mockMerchantService;
    private readonly Mock<IRecurringExpenseService> _mockRecurringService;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;

    private readonly Guid _account1Id = Guid.NewGuid();
    private readonly Guid _account2Id = Guid.NewGuid();
    private readonly Guid _catFoodId = Guid.NewGuid();
    private readonly Guid _catTransportId = Guid.NewGuid();

    public TransactionsPageFilteringTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        _mockStatusService = new Mock<ITransactionStatusService>();
        _mockStatusService.Setup(s => s.GetAllStatusesAsync()).ReturnsAsync(new List<TransactionCustomStatus>());

        _mockUiSettings = new Mock<IUiSettingsService>();
        _mockUiSettings.Setup(u => u.GetTableColumnsConfigAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

        _mockRulesEngine = new Mock<IRulesEngineService>();
        _mockMerchantService = new Mock<IMerchantService>();
        _mockMerchantService.Setup(m => m.GetAllMerchantsAsync(default)).ReturnsAsync(new List<Merchant>());

        _mockRecurringService = new Mock<IRecurringExpenseService>();
        _mockRecurringService.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<RecurringExpense>());

        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();
        _mockLocalizedEntityService
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var bank = new Bank { UserId = "test-user-id", Name = "BNP" };
        _context.Banks.Add(bank);

        var acc1 = new BankAccount { Id = _account1Id, UserId = "test-user-id", BankId = bank.Id, Name = "Compte A", InitialBalance = 100m };
        var acc2 = new BankAccount { Id = _account2Id, UserId = "test-user-id", BankId = bank.Id, Name = "Compte B", InitialBalance = 200m };
        _context.BankAccounts.AddRange(acc1, acc2);

        var catFood = new Category { Id = _catFoodId, UserId = "test-user-id", Name = "Nourriture" };
        var catTransport = new Category { Id = _catTransportId, UserId = "test-user-id", Name = "Transports" };
        _context.Categories.AddRange(catFood, catTransport);

        _context.Transactions.AddRange(
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = _account1Id,
                CategoryId = _catFoodId,
                Date = DateTime.Today.AddDays(-10),
                Amount = 45m,
                Type = TransactionType.Debit,
                Label = "Boulangerie Paul"
            },
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = _account1Id,
                CategoryId = _catTransportId,
                Date = DateTime.Today.AddDays(-5),
                Amount = 30m,
                Type = TransactionType.Debit,
                Label = "SNCF Billet"
            },
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = _account2Id,
                Date = DateTime.Today.AddDays(-2),
                Amount = 1500m,
                Type = TransactionType.Credit,
                Label = "Salaire Virement"
            }
        );

        _context.SaveChanges();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _context.Dispose();
        _connection.Dispose();
    }

    private TransactionsModel CreateModel()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        var pageContext = new PageContext(actionContext);

        return new TransactionsModel(
            _context,
            _mockStatusService.Object,
            _mockUiSettings.Object,
            _mockRulesEngine.Object,
            _mockMerchantService.Object,
            _mockRecurringService.Object,
            _mockLocalizedEntityService.Object,
            TimeProvider.System)
        {
            PageContext = pageContext,
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>())
        };
    }

    [Fact]
    public async Task OnGetAsync_WithoutFilters_ShouldReturnAllTransactions()
    {
        var model = CreateModel();

        await model.OnGetAsync();

        model.Transactions.Should().HaveCount(3);
    }

    [Fact]
    public async Task OnGetAsync_WithAccountIdFilter_ShouldFilterByAccount()
    {
        var model = CreateModel();
        model.AccountId = _account1Id;

        await model.OnGetAsync();

        model.Transactions.Should().HaveCount(2);
        model.Transactions.Should().OnlyContain(t => t.BankAccountId == _account1Id);
    }

    [Fact]
    public async Task OnGetAsync_WithSearchTerm_ShouldFilterMatchingLabel()
    {
        var model = CreateModel();
        model.SearchTerm = "Boulangerie";

        await model.OnGetAsync();

        model.Transactions.Should().HaveCount(1);
        model.Transactions[0].Label.Should().Be("Boulangerie Paul");
    }

    [Fact]
    public async Task OnGetAsync_WithAmountFilterBetween_ShouldFilterCorrectRange()
    {
        var model = CreateModel();
        model.AmountOperator = "between";
        model.AmountValue = 20m;
        model.AmountMaxValue = 40m;

        await model.OnGetAsync();

        model.Transactions.Should().HaveCount(1);
        model.Transactions[0].Amount.Should().Be(30m);
    }

    [Fact]
    public async Task OnGetAsync_WithTypeFilterDebit_ShouldReturnOnlyDebits()
    {
        var model = CreateModel();
        model.Type = TransactionType.Debit;

        await model.OnGetAsync();

        model.Transactions.Should().HaveCount(2);
        model.Transactions.Should().OnlyContain(t => t.Type == TransactionType.Debit);
    }

    [Fact]
    public void ActiveFilterCriteria_WhenFiltersToggled_UpdatesCountAndHasActiveFiltersCorrectly()
    {
        var model = CreateModel();

        // 1. Initial : aucun filtre actif
        model.HasActiveFilters.Should().BeFalse();
        model.ActiveFilterCriteriaCount.Should().Be(0);

        // 2. SearchTerm
        model.SearchTerm = "boulangerie";
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(0); // SearchTerm is general, not in criteria count
        model.SearchTerm = null;

        // 3. AccountId
        model.AccountId = _account1Id;
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.AccountId = null;

        // 4. CategoryId
        model.CategoryId = _catFoodId;
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.CategoryId = null;

        // 5. Type
        model.Type = TransactionType.Debit;
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.Type = null;

        // 6. StartDate / EndDate
        model.StartDate = DateTime.Today.AddDays(-10);
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.StartDate = null;
        model.EndDate = DateTime.Today;
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.EndDate = null;

        // 7. AmountValue & AmountOperator
        model.AmountValue = 50m;
        model.AmountOperator = "gt";
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.AmountValue = null;
        model.AmountOperator = null;

        // 8. RuleFilter
        model.RuleFilter = "matched";
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.RuleFilter = "all";
        model.HasActiveFilters.Should().BeFalse();

        // 9. RecurringFilter
        model.RecurringFilter = "recurring";
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.RecurringFilter = "all";
        model.HasActiveFilters.Should().BeFalse();

        // 10. AnomalyOnly
        model.AnomalyOnly = true;
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.AnomalyOnly = false;

        // 11. Multiple concurrent filters
        model.AccountId = _account1Id;
        model.CategoryId = _catFoodId;
        model.Type = TransactionType.Debit;
        model.StartDate = DateTime.Today;
        model.AmountValue = 20m;
        model.AmountOperator = "eq";
        model.RuleFilter = "matched";
        model.RecurringFilter = "ignored";
        model.AnomalyOnly = true;
        model.ActiveFilterCriteriaCount.Should().Be(8);
    }

    [Fact]
    public async Task OnPostUpdateStatusAsync_ValidTransaction_UpdatesStatus()
    {
        var model = CreateModel();
        var tx = await _context.Transactions.AsNoTracking().FirstAsync();
        var status = new TransactionCustomStatus
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Pointée",
            Code = "Reconciled",
            ColorHex = "#10b981",
            IconName = "bi-check2-all"
        };
        _context.TransactionStatuses.Add(status);
        await _context.SaveChangesAsync();
        _mockStatusService.Setup(s => s.GetByIdAsync(status.Id)).ReturnsAsync(status);

        // Act (AJAX header)
        model.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";
        var result = await model.OnPostUpdateStatusAsync(tx.Id, status.Id);

        result.Should().BeOfType<JsonResult>();
        var refreshedTx = await _context.Transactions.FindAsync(tx.Id);
        refreshedTx!.CustomStatusId.Should().Be(status.Id);
        refreshedTx.Status.Should().Be(TransactionStatus.Reconciled);

        // Error branches
        (await model.OnPostUpdateStatusAsync(Guid.NewGuid(), status.Id)).Should().BeOfType<NotFoundResult>();
        (await model.OnPostUpdateStatusAsync(tx.Id, Guid.NewGuid())).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OnPostBulkUpdateStatusAsync_ValidSelection_UpdatesAll()
    {
        var model = CreateModel();
        var txIds = await _context.Transactions.Select(t => t.Id).Take(2).ToListAsync();
        var status = new TransactionCustomStatus
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "En cours",
            Code = "Pending",
            ColorHex = "#f59e0b",
            IconName = "bi-hourglass"
        };
        _context.TransactionStatuses.Add(status);
        await _context.SaveChangesAsync();
        _mockStatusService.Setup(s => s.GetByIdAsync(status.Id)).ReturnsAsync(status);

        // Error branches
        (await model.OnPostBulkUpdateStatusAsync(new List<Guid>(), status.Id)).Should().BeOfType<BadRequestObjectResult>();
        (await model.OnPostBulkUpdateStatusAsync(txIds, Guid.NewGuid())).Should().BeOfType<BadRequestObjectResult>();

        // Valid AJAX
        model.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";
        var result = await model.OnPostBulkUpdateStatusAsync(txIds, status.Id);

        result.Should().BeOfType<JsonResult>();
        var updatedTxs = await _context.Transactions.Where(t => txIds.Contains(t.Id)).ToListAsync();
        updatedTxs.Should().OnlyContain(t => t.CustomStatusId == status.Id && t.Status == TransactionStatus.Pending);
    }

    [Fact]
    public async Task OnPostSaveColumnsConfigAsync_WhenInvoked_SavesConfiguration()
    {
        var model = CreateModel();

        (await model.OnPostSaveColumnsConfigAsync(null!)).Should().BeOfType<BadRequestObjectResult>();
        (await model.OnPostSaveColumnsConfigAsync(new TransactionsModel.ColumnConfigDto { ConfigJson = "" })).Should().BeOfType<BadRequestObjectResult>();

        var validDto = new TransactionsModel.ColumnConfigDto
        {
            TableKey = "Transactions",
            ConfigJson = "{\"colDate\":true}"
        };

        var result = await model.OnPostSaveColumnsConfigAsync(validDto);
        result.Should().BeOfType<JsonResult>();
        _mockUiSettings.Verify(u => u.SetTableColumnsConfigAsync("Transactions", validDto.ConfigJson), Times.Once);
    }

    [Fact]
    public async Task OnPostCreateRuleFromTransactionAsync_WhenValid_ValidatesAndCreatesRule()
    {
        var model = CreateModel();

        // 1. Validation errors
        (await model.OnPostCreateRuleFromTransactionAsync(null!)).Should().BeOfType<BadRequestObjectResult>();
        (await model.OnPostCreateRuleFromTransactionAsync(new TransactionsModel.QuickRuleDto { Name = "", Pattern = "CARREFOUR", TargetCategoryId = _catFoodId }))
            .Should().BeOfType<BadRequestObjectResult>();
        (await model.OnPostCreateRuleFromTransactionAsync(new TransactionsModel.QuickRuleDto { Name = "Rule 1", Pattern = "[invalid-regex(", TargetCategoryId = _catFoodId }))
            .Should().BeOfType<BadRequestObjectResult>();

        // 2. Valid rule creation with immediate application
        _mockRulesEngine
            .Setup(r => r.ApplyRulesToDatabaseAsync(It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var validDto = new TransactionsModel.QuickRuleDto
        {
            Name = "Regle Carrefour",
            Pattern = "CARREFOUR*",
            TargetCategoryId = _catFoodId,
            ApplyImmediately = true,
            OverwriteExisting = false
        };

        var result = await model.OnPostCreateRuleFromTransactionAsync(validDto);
        result.Should().BeOfType<JsonResult>();

        _context.CategorizationRules.Should().ContainSingle(r => r.Name == "Regle Carrefour" && r.TargetCategoryId == _catFoodId);
        _mockRulesEngine.Verify(r => r.ApplyRulesToDatabaseAsync(false, null, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);

        // 3. Rule creation with TargetMerchantId
        var merchant = new Ledger.Features.Merchants.Entities.Merchant { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Carrefour", NormalizedName = "CARREFOUR" };
        _context.Merchants.Add(merchant);
        await _context.SaveChangesAsync();

        var withMerchantDto = new TransactionsModel.QuickRuleDto
        {
            Name = "Regle Carrefour Market",
            Pattern = "CARREFOUR MARKET*",
            TargetCategoryId = _catFoodId,
            TargetMerchantId = merchant.Id
        };
        var resMerchant = await model.OnPostCreateRuleFromTransactionAsync(withMerchantDto);
        resMerchant.Should().BeOfType<JsonResult>();
        var ruleWithMerchant = await _context.CategorizationRules.FirstOrDefaultAsync(r => r.Name == "Regle Carrefour Market");
        ruleWithMerchant.Should().NotBeNull();
        ruleWithMerchant.TargetMerchantId.Should().Be(merchant.Id);

        // 4. Rule creation with NewMerchantName (auto-creation)
        var newMerchant = new Ledger.Features.Merchants.Entities.Merchant { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Monoprix", NormalizedName = "MONOPRIX" };
        _context.Merchants.Add(newMerchant);
        await _context.SaveChangesAsync();
        _mockMerchantService
            .Setup(m => m.GetOrCreateByNameAsync("Monoprix", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newMerchant);

        var withNewMerchantDto = new TransactionsModel.QuickRuleDto
        {
            Name = "Regle Monoprix",
            Pattern = "MONOPRIX*",
            TargetCategoryId = _catFoodId,
            NewMerchantName = "Monoprix"
        };
        var resNewMerchant = await model.OnPostCreateRuleFromTransactionAsync(withNewMerchantDto);
        resNewMerchant.Should().BeOfType<JsonResult>();
        var ruleWithNewMerchant = await _context.CategorizationRules.FirstOrDefaultAsync(r => r.Name == "Regle Monoprix");
        ruleWithNewMerchant.Should().NotBeNull();
        ruleWithNewMerchant.TargetMerchantId.Should().Be(newMerchant.Id);
    }

    [Fact]
    public async Task OnPostCreateRecurringFromTransactionAsync_WhenValid_ValidatesAndCreatesRecurring()
    {
        var model = CreateModel();

        // 1. Validation errors
        (await model.OnPostCreateRecurringFromTransactionAsync(null!)).Should().BeOfType<BadRequestObjectResult>();
        (await model.OnPostCreateRecurringFromTransactionAsync(new TransactionsModel.CreateRecurringDto { Name = "EDF", Pattern = "EDF*", ExpectedAmount = 0 }))
            .Should().BeOfType<BadRequestObjectResult>();

        // 2. Valid creation
        var createdRecurring = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Electricite EDF",
            Pattern = "EDF*",
            ExpectedAmount = 95m,
            IsActive = true
        };

        _mockRecurringService
            .Setup(r => r.ConfirmCandidateAsync(It.IsAny<Ledger.Features.Recurring.Models.RecurringCandidateDto>()))
            .ReturnsAsync(createdRecurring);

        var validDto = new TransactionsModel.CreateRecurringDto
        {
            Name = "Electricite EDF",
            Pattern = "EDF*",
            ExpectedAmount = 95m,
            AmountTolerancePercentage = 0.10m,
            Notes = "Prélèvement mensuel le 15"
        };

        var result = await model.OnPostCreateRecurringFromTransactionAsync(validDto);
        result.Should().BeOfType<JsonResult>();

        _mockRecurringService.Verify(r => r.ConfirmCandidateAsync(It.Is<Ledger.Features.Recurring.Models.RecurringCandidateDto>(c => c.SuggestedName == "Electricite EDF")), Times.Once);
        _mockRecurringService.Verify(r => r.UpdateAsync(It.Is<RecurringExpense>(re => re.Notes == "Prélèvement mensuel le 15")), Times.Once);
    }
}
