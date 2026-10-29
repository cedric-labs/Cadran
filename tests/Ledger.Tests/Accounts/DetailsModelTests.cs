using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Pages;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Entities;
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

public sealed class DetailsModelTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<IAccountBalanceService> _mockBalanceService;
    private readonly Mock<IRulesEngineService> _mockRulesEngine;
    private readonly Mock<ITransactionStatusService> _mockStatusService;
    private readonly Mock<IUiSettingsService> _mockUiSettings;
    private readonly Mock<IRecurringExpenseService> _mockRecurringService;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly Mock<IMerchantService> _mockMerchantService;
    private readonly Guid _bankAccountId;

    public DetailsModelTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        _mockBalanceService = new Mock<IAccountBalanceService>();
        _mockBalanceService
            .Setup(b => b.GetAccountBalanceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountBalanceSummaryDto(Guid.NewGuid(), "Compte Principal", "LCL", AccountType.Checking, 1000m, 1200m, 800m, "EUR"));

        _mockRulesEngine = new Mock<IRulesEngineService>();
        _mockStatusService = new Mock<ITransactionStatusService>();
        _mockStatusService.Setup(s => s.GetAllStatusesAsync()).ReturnsAsync(new List<TransactionCustomStatus>());

        _mockUiSettings = new Mock<IUiSettingsService>();
        _mockUiSettings.Setup(u => u.GetTableColumnsConfigAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

        _mockRecurringService = new Mock<IRecurringExpenseService>();
        _mockRecurringService.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<RecurringExpense>());

        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();
        _mockLocalizedEntityService
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        _mockMerchantService = new Mock<IMerchantService>();
        _mockMerchantService.Setup(m => m.GetAllMerchantsAsync(default)).ReturnsAsync(new List<Ledger.Features.Merchants.Entities.Merchant>());

        var bank = new Bank { UserId = "test-user-id", Name = "LCL" };
        _context.Banks.Add(bank);

        _bankAccountId = Guid.NewGuid();
        var account = new BankAccount
        {
            Id = _bankAccountId,
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Principal",
            Type = AccountType.Checking,
            InitialBalance = 500m
        };
        _context.BankAccounts.Add(account);

        var cat1 = new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Alimentation" };
        var cat2 = new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Transport" };
        _context.Categories.AddRange(cat1, cat2);

        _context.Transactions.AddRange(
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = _bankAccountId,
                CategoryId = cat1.Id,
                Date = DateTime.Today.AddDays(-5),
                Amount = 55.40m,
                Type = TransactionType.Debit,
                Label = "Supermarché Match"
            },
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = _bankAccountId,
                CategoryId = cat2.Id,
                Date = DateTime.Today.AddDays(-2),
                Amount = 15.00m,
                Type = TransactionType.Debit,
                Label = "Ticket Métro"
            },
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = _bankAccountId,
                Date = DateTime.Today.AddDays(-1),
                Amount = 2000.00m,
                Type = TransactionType.Credit,
                Label = "Virement Employeur"
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

    private DetailsModel CreateModel()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        var pageContext = new PageContext(actionContext);

        var model = new DetailsModel(
            _context,
            _mockBalanceService.Object,
            _mockRulesEngine.Object,
            _mockStatusService.Object,
            _mockUiSettings.Object,
            _mockRecurringService.Object,
            _mockLocalizedEntityService.Object,
            _mockMerchantService.Object)
        {
            PageContext = pageContext,
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>())
        };

        return model;
    }

    [Fact]
    public async Task OnGetAsync_WithValidAccountId_ShouldLoadAccountAndTransactions()
    {
        var model = CreateModel();

        var result = await model.OnGetAsync(_bankAccountId);

        result.Should().BeOfType<PageResult>();
        model.Account.Should().NotBeNull();
        model.Account.Name.Should().Be("Compte Principal");
        model.Transactions.Should().HaveCount(3);
    }

    [Fact]
    public async Task OnGetAsync_WithSearchTerm_ShouldFilterTransactions()
    {
        var model = CreateModel();
        model.SearchTerm = "Match";

        await model.OnGetAsync(_bankAccountId);

        model.Transactions.Should().HaveCount(1);
        model.Transactions[0].Label.Should().Be("Supermarché Match");
    }

    [Fact]
    public async Task OnGetAsync_WithTypeFilter_ShouldFilterCreditTransactions()
    {
        var model = CreateModel();
        model.Type = TransactionType.Credit;

        await model.OnGetAsync(_bankAccountId);

        model.Transactions.Should().HaveCount(1);
        model.Transactions[0].Amount.Should().Be(2000.00m);
    }

    [Fact]
    public async Task OnGetAsync_WithAmountFilterGreaterThan_FiltersTransactions()
    {
        var model = CreateModel();
        model.AmountOperator = "gt";
        model.AmountValue = 100m;

        await model.OnGetAsync(_bankAccountId);

        model.Transactions.Should().HaveCount(1);
        model.Transactions[0].Amount.Should().Be(2000.00m);
    }

    [Fact]
    public async Task OnGetAsync_WhenAccountNotFound_ShouldReturnNotFound()
    {
        var model = CreateModel();

        var result = await model.OnGetAsync(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OnPostDeleteTransactionAsync_WhenInvoked_RemovesTransaction()
    {
        var model = CreateModel();
        var tx = await _context.Transactions.FirstAsync(t => t.BankAccountId == _bankAccountId);

        var result = await model.OnPostDeleteTransactionAsync(tx.Id);

        result.Should().BeOfType<RedirectResult>();
        (await _context.Transactions.AnyAsync(t => t.Id == tx.Id)).Should().BeFalse();
    }

    [Fact]
    public void ActiveFilterCriteria_WhenFiltersToggled_UpdatesCountAndHasActiveFiltersCorrectly()
    {
        var model = CreateModel();

        model.HasActiveFilters.Should().BeFalse();
        model.ActiveFilterCriteriaCount.Should().Be(0);

        model.SearchTerm = "match";
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(0);
        model.SearchTerm = null;

        model.CategoryId = Guid.NewGuid();
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.CategoryId = null;

        model.Type = TransactionType.Debit;
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.Type = null;

        model.StartDate = DateTime.Today.AddDays(-10);
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.StartDate = null;
        model.EndDate = DateTime.Today;
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.EndDate = null;

        model.AmountValue = 50m;
        model.AmountOperator = "gt";
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.AmountValue = null;
        model.AmountOperator = null;

        model.RuleFilter = "matched";
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.RuleFilter = "all";
        model.HasActiveFilters.Should().BeFalse();

        model.RecurringFilter = "recurring";
        model.HasActiveFilters.Should().BeTrue();
        model.ActiveFilterCriteriaCount.Should().Be(1);
        model.RecurringFilter = "all";
        model.HasActiveFilters.Should().BeFalse();
    }

    [Fact]
    public async Task OnPostUpdateStatusAsync_ValidTransaction_UpdatesStatus()
    {
        var model = CreateModel();
        var tx = await _context.Transactions.AsNoTracking().FirstAsync(t => t.BankAccountId == _bankAccountId);
        var status = new TransactionCustomStatus
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Vérifiée",
            Code = "Cleared",
            ColorHex = "#3b82f6",
            IconName = "bi-check"
        };
        _context.TransactionStatuses.Add(status);
        await _context.SaveChangesAsync();
        _mockStatusService.Setup(s => s.GetByIdAsync(status.Id)).ReturnsAsync(status);

        // AJAX request
        model.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";
        var result = await model.OnPostUpdateStatusAsync(tx.Id, status.Id);

        result.Should().BeOfType<JsonResult>();
        var refreshedTx = await _context.Transactions.FindAsync(tx.Id);
        refreshedTx!.CustomStatusId.Should().Be(status.Id);
        refreshedTx.Status.Should().Be(TransactionStatus.Cleared);

        // Error branches
        (await model.OnPostUpdateStatusAsync(Guid.NewGuid(), status.Id)).Should().BeOfType<NotFoundResult>();
        (await model.OnPostUpdateStatusAsync(tx.Id, Guid.NewGuid())).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OnPostBulkUpdateStatusAsync_ValidSelection_UpdatesTransactions()
    {
        var model = CreateModel();
        var txIds = await _context.Transactions.Where(t => t.BankAccountId == _bankAccountId).Select(t => t.Id).Take(2).ToListAsync();
        var status = new TransactionCustomStatus
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Clôturé",
            Code = "Reconciled",
            ColorHex = "#10b981",
            IconName = "bi-check-all"
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
        var updated = await _context.Transactions.Where(t => txIds.Contains(t.Id)).ToListAsync();
        updated.Should().OnlyContain(t => t.CustomStatusId == status.Id && t.Status == TransactionStatus.Reconciled);
    }

    [Fact]
    public async Task OnPostBulkDeleteTransactionsAsync_ValidSelection_RemovesTransactions()
    {
        var model = CreateModel();
        var txIds = await _context.Transactions.Where(t => t.BankAccountId == _bankAccountId).Select(t => t.Id).Take(2).ToListAsync();

        // Error branch
        (await model.OnPostBulkDeleteTransactionsAsync(new List<Guid>())).Should().BeOfType<BadRequestObjectResult>();

        // Valid AJAX
        model.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";
        var result = await model.OnPostBulkDeleteTransactionsAsync(txIds);

        result.Should().BeOfType<JsonResult>();
        (await _context.Transactions.Where(t => txIds.Contains(t.Id)).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OnPostApplyRulesAsync_WhenInvoked_AppliesRulesSuccessfully()
    {
        var model = CreateModel();
        _mockRulesEngine
            .Setup(r => r.ApplyRulesToDatabaseAsync(false, _bankAccountId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var result = await model.OnPostApplyRulesAsync(_bankAccountId);

        result.Should().BeOfType<RedirectToPageResult>();
        _mockRulesEngine.Verify(r => r.ApplyRulesToDatabaseAsync(false, _bankAccountId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnPostSaveColumnsConfigAsync_WhenInvoked_SavesConfiguration()
    {
        var model = CreateModel();

        (await model.OnPostSaveColumnsConfigAsync(null!)).Should().BeOfType<BadRequestObjectResult>();
        (await model.OnPostSaveColumnsConfigAsync(new DetailsModel.ColumnConfigDto { ConfigJson = "" })).Should().BeOfType<BadRequestObjectResult>();

        var validDto = new DetailsModel.ColumnConfigDto
        {
            TableKey = "AccountDetails",
            ConfigJson = "{\"colAmount\":true}"
        };

        var result = await model.OnPostSaveColumnsConfigAsync(validDto);
        result.Should().BeOfType<JsonResult>();
        _mockUiSettings.Verify(u => u.SetTableColumnsConfigAsync("AccountDetails", validDto.ConfigJson), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_WithAdditionalFilters_FiltersCorrectly()
    {
        var model = CreateModel();

        // 1. AmountOperator = lt
        model.AmountOperator = "lt";
        model.AmountValue = 20m;
        await model.OnGetAsync(_bankAccountId);
        model.Transactions.Should().HaveCount(1);
        model.Transactions[0].Amount.Should().Be(15m);

        // 2. AmountOperator = between
        model.AmountOperator = "between";
        model.AmountValue = 50m;
        model.AmountMaxValue = 60m;
        await model.OnGetAsync(_bankAccountId);
        model.Transactions.Should().HaveCount(1);
        model.Transactions[0].Amount.Should().Be(55.40m);

        // 3. CategoryId
        var cat = await _context.Categories.FirstAsync(c => c.Name == "Alimentation");
        model.AmountOperator = null;
        model.AmountValue = null;
        model.AmountMaxValue = null;
        model.CategoryId = cat.Id;
        await model.OnGetAsync(_bankAccountId);
        model.Transactions.Should().HaveCount(1);
        model.Transactions[0].Label.Should().Be("Supermarché Match");
    }

    [Fact]
    public async Task OnPostUpdateCategoryAsync_ValidInputs_UpdatesTransactionAndSimilarLabels()
    {
        var model = CreateModel();
        var cat = await _context.Categories.FirstAsync(c => c.Name == "Alimentation");
        var tx = await _context.Transactions.FirstAsync(t => t.Label == "Supermarché Match");

        var merchant = new Ledger.Features.Merchants.Entities.Merchant
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Match Express",
            NormalizedName = "MATCH EXPRESS"
        };
        _context.Merchants.Add(merchant);
        await _context.SaveChangesAsync();

        _mockMerchantService.Setup(m => m.GetOrCreateByNameAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(merchant);
        _mockMerchantService.Setup(m => m.GetByIdAsync(merchant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(merchant);

        // AJAX request
        model.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";
        var result = await model.OnPostUpdateCategoryAsync(
            transactionId: tx.Id,
            newCategoryId: cat.Id,
            merchantId: null,
            newMerchantName: "Match Express",
            budgetDate: DateTime.Today.AddMonths(1),
            notes: "Note test",
            statusId: null,
            status: TransactionStatus.Cleared,
            applyToSimilarLabels: true);

        result.Should().BeOfType<JsonResult>();
        var updatedTx = await _context.Transactions.FindAsync(tx.Id);
        updatedTx!.CategoryId.Should().Be(cat.Id);
        updatedTx.Notes.Should().Be("Note test");
        updatedTx.Status.Should().Be(TransactionStatus.Cleared);
    }

    [Fact]
    public async Task OnPostUpdateCategoryAsync_ErrorCases_ReturnsNotFoundOrBadRequest()
    {
        var model = CreateModel();

        // 1. Transaction inexistante -> NotFound
        var resNotFound = await model.OnPostUpdateCategoryAsync(
            transactionId: Guid.NewGuid(),
            newCategoryId: null,
            merchantId: null,
            newMerchantName: null,
            budgetDate: null,
            notes: null,
            statusId: null,
            status: null,
            applyToSimilarLabels: false);
        resNotFound.Should().BeOfType<NotFoundResult>();

        // 2. Catégorie cible inexistante -> BadRequest
        var tx = await _context.Transactions.FirstAsync();
        var resBadCat = await model.OnPostUpdateCategoryAsync(
            transactionId: tx.Id,
            newCategoryId: Guid.NewGuid(),
            merchantId: null,
            newMerchantName: null,
            budgetDate: null,
            notes: null,
            statusId: null,
            status: null,
            applyToSimilarLabels: false);
        resBadCat.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OnPostCreateRuleFromTransactionAsync_ValidAndInvalidBranches_HandledCorrectly()
    {
        var model = CreateModel();
        var cat = await _context.Categories.FirstAsync();

        // 1. Dto invalide -> BadRequest
        var resNull = await model.OnPostCreateRuleFromTransactionAsync(null!);
        resNull.Should().BeOfType<BadRequestObjectResult>();

        // 2. Regex invalide -> BadRequest
        var badRegexDto = new DetailsModel.QuickRuleDto
        {
            Name = "Bad Rule",
            Pattern = "[unclosed regex",
            TargetCategoryId = cat.Id
        };
        var resBadRegex = await model.OnPostCreateRuleFromTransactionAsync(badRegexDto);
        resBadRegex.Should().BeOfType<BadRequestObjectResult>();

        // 3. Règle valide avec ApplyImmediately = true
        _mockRulesEngine.Setup(r => r.ApplyRulesToDatabaseAsync(It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var validDto = new DetailsModel.QuickRuleDto
        {
            Name = "Règle Supermarché",
            Pattern = "MATCH",
            TargetCategoryId = cat.Id,
            ApplyImmediately = true
        };
        var resValid = await model.OnPostCreateRuleFromTransactionAsync(validDto);
        resValid.Should().BeOfType<JsonResult>();

        // 4. Règle avec TargetMerchantId
        var merchant = new Ledger.Features.Merchants.Entities.Merchant { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Auchan", NormalizedName = "AUCHAN" };
        _context.Merchants.Add(merchant);
        await _context.SaveChangesAsync();

        var withMerchantDto = new DetailsModel.QuickRuleDto
        {
            Name = "Règle Auchan",
            Pattern = "AUCHAN*",
            TargetCategoryId = cat.Id,
            TargetMerchantId = merchant.Id
        };
        var resMerchant = await model.OnPostCreateRuleFromTransactionAsync(withMerchantDto);
        resMerchant.Should().BeOfType<JsonResult>();
        var createdAuchan = await _context.CategorizationRules.FirstOrDefaultAsync(r => r.Name == "Règle Auchan");
        createdAuchan.Should().NotBeNull();
        createdAuchan.TargetMerchantId.Should().Be(merchant.Id);

        // 5. Règle avec NewMerchantName (auto-création)
        var newMerchant = new Ledger.Features.Merchants.Entities.Merchant { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Boulanger", NormalizedName = "BOULANGER" };
        _context.Merchants.Add(newMerchant);
        await _context.SaveChangesAsync();
        _mockMerchantService
            .Setup(m => m.GetOrCreateByNameAsync("Boulanger", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newMerchant);

        var withNewMerchantDto = new DetailsModel.QuickRuleDto
        {
            Name = "Règle Boulanger",
            Pattern = "BOULANGER*",
            TargetCategoryId = cat.Id,
            NewMerchantName = "Boulanger"
        };
        var resNewMerchant = await model.OnPostCreateRuleFromTransactionAsync(withNewMerchantDto);
        resNewMerchant.Should().BeOfType<JsonResult>();
        var createdBoulanger = await _context.CategorizationRules.FirstOrDefaultAsync(r => r.Name == "Règle Boulanger");
        createdBoulanger.Should().NotBeNull();
        createdBoulanger.TargetMerchantId.Should().Be(newMerchant.Id);
    }

    [Fact]
    public async Task OnPostCreateRecurringFromTransactionAsync_ValidAndInvalidBranches_HandledCorrectly()
    {
        var model = CreateModel();
        var cat = await _context.Categories.FirstAsync();

        // 1. Dto invalide -> BadRequest
        var resNull = await model.OnPostCreateRecurringFromTransactionAsync(null!);
        resNull.Should().BeOfType<BadRequestObjectResult>();

        // 2. Dto valide
        _mockRecurringService.Setup(r => r.ConfirmCandidateAsync(It.IsAny<Ledger.Features.Recurring.Models.RecurringCandidateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ledger.Features.Recurring.Models.RecurringCandidateDto c, CancellationToken _) => new RecurringExpense
            {
                Id = Guid.NewGuid(),
                UserId = "test-user-id",
                Name = c.SuggestedName,
                Pattern = c.Pattern,
                ExpectedAmount = c.AverageAmount
            });

        var validDto = new DetailsModel.CreateRecurringDto
        {
            Name = "Abonnement Métro",
            Pattern = "METRO",
            ExpectedAmount = 15m,
            ExpectedDayOfMonth = 1,
            Frequency = RecurringFrequency.Monthly,
            TargetCategoryId = cat.Id,
            AmountTolerancePercentage = 0.10m,
            Notes = "Recharge mensuelle Navigo"
        };

        var resValid = await model.OnPostCreateRecurringFromTransactionAsync(validDto);
        resValid.Should().BeOfType<JsonResult>();
    }

    [Fact]
    public async Task OnPostSaveAndRemoveSplitsAsync_WhenInvoked_WorksCorrectly()
    {
        var tx = new Transaction
        {
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 50m,
            Type = TransactionType.Debit,
            Label = "Test Split"
        };
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();
        var model = CreateModel();

        // 1. Dto null
        var resNull = await model.OnPostSaveTransactionSplitsAsync(null!);
        resNull.Should().BeOfType<BadRequestObjectResult>();

        // 2. Tx introuvable
        var resNotFound = await model.OnPostSaveTransactionSplitsAsync(new DetailsModel.SaveSplitsDto { TransactionId = Guid.NewGuid() });
        resNotFound.Should().BeOfType<NotFoundObjectResult>();

        // 3. Moins de 2 parts
        var resLess2 = await model.OnPostSaveTransactionSplitsAsync(new DetailsModel.SaveSplitsDto
        {
            TransactionId = tx.Id,
            Splits = new List<DetailsModel.SplitLineDto> { new() { Amount = 50m } }
        });
        resLess2.Should().BeOfType<BadRequestObjectResult>();

        // 4. Somme incorrecte (40m != 50m)
        var resWrongSum = await model.OnPostSaveTransactionSplitsAsync(new DetailsModel.SaveSplitsDto
        {
            TransactionId = tx.Id,
            Splits = new List<DetailsModel.SplitLineDto>
            {
                new() { Amount = 20m },
                new() { Amount = 20m }
            }
        });
        resWrongSum.Should().BeOfType<BadRequestObjectResult>();

        // 5. Cas valide (30m + 20m = 50m)
        var resValid = await model.OnPostSaveTransactionSplitsAsync(new DetailsModel.SaveSplitsDto
        {
            TransactionId = tx.Id,
            Splits = new List<DetailsModel.SplitLineDto>
            {
                new() { Amount = 30m, Notes = "Part 1" },
                new() { Amount = 20m, Notes = "Part 2" }
            }
        });
        resValid.Should().BeOfType<JsonResult>();

        await _context.Entry(tx).ReloadAsync();
        tx.IsSplit.Should().BeTrue();
        _context.TransactionSplits.Where(s => s.TransactionId == tx.Id).Should().HaveCount(2);

        // 6. Suppression des ventilations
        var resRemoveNotFound = await model.OnPostRemoveTransactionSplitsAsync(Guid.NewGuid());
        resRemoveNotFound.Should().BeOfType<NotFoundObjectResult>();

        var resRemove = await model.OnPostRemoveTransactionSplitsAsync(tx.Id);
        resRemove.Should().BeOfType<JsonResult>();

        await _context.Entry(tx).ReloadAsync();
        tx.IsSplit.Should().BeFalse();
        _context.TransactionSplits.Where(s => s.TransactionId == tx.Id).Should().BeEmpty();
    }
}
