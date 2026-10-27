using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Pages;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Services;
using Ledger.Features.Recurring.Services;
using Ledger.Features.RulesEngine.Services;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Ledger.Tests.Accounts;

public class TransactionsSplitTests : IDisposable
{
    private readonly Ledger.Infrastructure.Data.AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Mock<ITransactionStatusService> _mockStatusService;
    private readonly Mock<IUiSettingsService> _mockUiSettingsService;
    private readonly Mock<IRulesEngineService> _mockRulesEngineService;
    private readonly Mock<IMerchantService> _mockMerchantService;
    private readonly Mock<IRecurringExpenseService> _mockRecurringService;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _catFoodId = Guid.NewGuid();
    private readonly Guid _catHousingId = Guid.NewGuid();

    public TransactionsSplitTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _mockStatusService = new Mock<ITransactionStatusService>();
        _mockUiSettingsService = new Mock<IUiSettingsService>();
        _mockRulesEngineService = new Mock<IRulesEngineService>();
        _mockMerchantService = new Mock<IMerchantService>();
        _mockRecurringService = new Mock<IRecurringExpenseService>();
        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();

        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque Test" };
        var account = new BankAccount { Id = _bankAccountId, UserId = "test-user-id", BankId = bank.Id, Name = "Compte Courant" };
        var catFood = new Category { Id = _catFoodId, UserId = "test-user-id", Name = "Alimentation" };
        var catHousing = new Category { Id = _catHousingId, UserId = "test-user-id", Name = "Logement" };

        _context.Banks.Add(bank);
        _context.BankAccounts.Add(account);
        _context.Categories.AddRange(catFood, catHousing);
        _context.SaveChanges();
    }

    private TransactionsModel CreateModel()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        return new TransactionsModel(
            _context,
            _mockStatusService.Object,
            _mockUiSettingsService.Object,
            _mockRulesEngineService.Object,
            _mockMerchantService.Object,
            _mockRecurringService.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary()))
        };
    }

    [Fact]
    public async Task OnGetTransactionSplitsAsync_NonExistingTx_ReturnsNotFound()
    {
        // Arrange
        var model = CreateModel();

        // Act
        var result = await model.OnGetTransactionSplitsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task OnGetTransactionSplitsAsync_ExistingTx_ReturnsDetailsAndSplits()
    {
        // Arrange
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 100m,
            Label = "DEPENSE GLOBALE",
            Type = TransactionType.Debit
        };
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        var model = CreateModel();

        // Act
        var result = await model.OnGetTransactionSplitsAsync(tx.Id);

        // Assert
        result.Should().BeOfType<JsonResult>();
    }

    [Fact]
    public async Task OnPostSaveTransactionSplitsAsync_FewerThanTwoParts_ReturnsBadRequest()
    {
        // Arrange
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 100m,
            Label = "DEPENSE",
            Type = TransactionType.Debit
        };
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        var model = CreateModel();
        var dto = new TransactionsModel.SaveSplitsDto
        {
            TransactionId = tx.Id,
            Splits = new List<TransactionsModel.SplitLineDto>
            {
                new() { Amount = 100m, CategoryId = _catFoodId }
            }
        };

        // Act
        var result = await model.OnPostSaveTransactionSplitsAsync(dto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OnPostSaveTransactionSplitsAsync_UnbalancedSum_ReturnsBadRequest()
    {
        // Arrange
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 100m,
            Label = "DEPENSE",
            Type = TransactionType.Debit
        };
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        var model = CreateModel();
        var dto = new TransactionsModel.SaveSplitsDto
        {
            TransactionId = tx.Id,
            Splits = new List<TransactionsModel.SplitLineDto>
            {
                new() { Amount = 60m, CategoryId = _catFoodId },
                new() { Amount = 30m, CategoryId = _catHousingId } // Somme = 90 != 100
            }
        };

        // Act
        var result = await model.OnPostSaveTransactionSplitsAsync(dto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OnPostSaveTransactionSplitsAsync_BalancedSum_SavesSplitsAndSetsIsSplitTrue()
    {
        // Arrange
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 100m,
            Label = "DEPENSE SUPERMARCHE",
            Type = TransactionType.Debit
        };
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        var model = CreateModel();
        var dto = new TransactionsModel.SaveSplitsDto
        {
            TransactionId = tx.Id,
            Splits = new List<TransactionsModel.SplitLineDto>
            {
                new() { Amount = 65m, CategoryId = _catFoodId, Notes = "Courses" },
                new() { Amount = 35m, CategoryId = _catHousingId, Notes = "Produits d'entretien" }
            }
        };

        // Act
        var result = await model.OnPostSaveTransactionSplitsAsync(dto);

        // Assert
        result.Should().BeOfType<JsonResult>();

        var updatedTx = await _context.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == tx.Id);

        updatedTx.Should().NotBeNull();
        updatedTx!.IsSplit.Should().BeTrue();
        updatedTx.Splits.Should().HaveCount(2);
        updatedTx.Splits.Sum(s => s.Amount).Should().Be(100m);
    }

    [Fact]
    public async Task OnPostRemoveTransactionSplitsAsync_WhenSplitExists_DeSplitsAndRemovesAllParts()
    {
        // Arrange
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 100m,
            Label = "DEPENSE",
            Type = TransactionType.Debit,
            IsSplit = true
        };
        var s1 = new TransactionSplit { Id = Guid.NewGuid(), UserId = "test-user-id", TransactionId = tx.Id, Amount = 60m, CategoryId = _catFoodId };
        var s2 = new TransactionSplit { Id = Guid.NewGuid(), UserId = "test-user-id", TransactionId = tx.Id, Amount = 40m, CategoryId = _catHousingId };
        tx.Splits.Add(s1);
        tx.Splits.Add(s2);
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        var model = CreateModel();

        // Act
        var result = await model.OnPostRemoveTransactionSplitsAsync(tx.Id);

        // Assert
        result.Should().BeOfType<JsonResult>();

        var updatedTx = await _context.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == tx.Id);

        updatedTx.Should().NotBeNull();
        updatedTx!.IsSplit.Should().BeFalse();
        updatedTx.Splits.Should().BeEmpty();
    }

    private DetailsModel CreateDetailsModel()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";
        var mockBalanceService = new Mock<IAccountBalanceService>();
        mockBalanceService.Setup(b => b.GetAccountBalanceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountBalanceSummaryDto(_bankAccountId, "Compte Courant", "Banque Test", AccountType.Checking, 1000m, 1000m, 0m, "EUR"));

        _mockUiSettingsService.Setup(u => u.GetDefaultPeriodAsync()).ReturnsAsync("current_month");
        _mockUiSettingsService.Setup(u => u.GetTransactionsPageSizeAsync()).ReturnsAsync(50);
        _mockUiSettingsService.Setup(u => u.GetShowKpiCentsAsync()).ReturnsAsync(true);
        _mockUiSettingsService.Setup(u => u.GetSalaryMonthStartDayAsync()).ReturnsAsync(1);

        return new DetailsModel(
            _context,
            mockBalanceService.Object,
            _mockRulesEngineService.Object,
            _mockStatusService.Object,
            _mockUiSettingsService.Object,
            _mockRecurringService.Object,
            _mockLocalizedEntityService.Object,
            _mockMerchantService.Object)
        {
            PageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary()))
        };
    }

    [Fact]
    public async Task OnGetTransactionSplitsAsync_WhenTransactionExists_ReturnsDetailsAndSplits()
    {
        // Arrange
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 150m,
            Label = "ACHAT CENTRE COMMERCIAL",
            Type = TransactionType.Debit
        };
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        var model = CreateDetailsModel();

        // Act
        var result = await model.OnGetTransactionSplitsAsync(tx.Id);

        // Assert
        result.Should().BeOfType<JsonResult>();
    }

    [Fact]
    public async Task OnPostSaveTransactionSplitsAsync_WhenSumIsBalanced_SavesSplitsAndSetsIsSplitTrue()
    {
        // Arrange
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 80m,
            Label = "RETRAIT GAB",
            Type = TransactionType.Debit
        };
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        var model = CreateDetailsModel();
        var dto = new DetailsModel.SaveSplitsDto
        {
            TransactionId = tx.Id,
            Splits = new List<DetailsModel.SplitLineDto>
            {
                new() { Amount = 50m, CategoryId = _catFoodId, Notes = "Alimentation" },
                new() { Amount = 30m, CategoryId = _catHousingId, Notes = "Divers maison" }
            }
        };

        // Act
        var result = await model.OnPostSaveTransactionSplitsAsync(dto);

        // Assert
        result.Should().BeOfType<JsonResult>();

        var updatedTx = await _context.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == tx.Id);

        updatedTx.Should().NotBeNull();
        updatedTx!.IsSplit.Should().BeTrue();
        updatedTx.Splits.Should().HaveCount(2);
        updatedTx.Splits.Sum(s => s.Amount).Should().Be(80m);
    }

    [Fact]
    public async Task OnPostRemoveSplitsAsync_WhenSplitExists_DeSplitsAndRemovesAllParts()
    {
        // Arrange
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 80m,
            Label = "RETRAIT GAB",
            Type = TransactionType.Debit,
            IsSplit = true
        };
        var s1 = new TransactionSplit { Id = Guid.NewGuid(), UserId = "test-user-id", TransactionId = tx.Id, Amount = 50m, CategoryId = _catFoodId };
        var s2 = new TransactionSplit { Id = Guid.NewGuid(), UserId = "test-user-id", TransactionId = tx.Id, Amount = 30m, CategoryId = _catHousingId };
        tx.Splits.Add(s1);
        tx.Splits.Add(s2);
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        var model = CreateDetailsModel();

        // Act
        var result = await model.OnPostRemoveTransactionSplitsAsync(tx.Id);

        // Assert
        result.Should().BeOfType<JsonResult>();

        var updatedTx = await _context.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == tx.Id);

        updatedTx.Should().NotBeNull();
        updatedTx!.IsSplit.Should().BeFalse();
        updatedTx.Splits.Should().BeEmpty();
    }

    [Fact]
    public async Task DetailsModel_OnGetAsync_LoadsSplitsForAccountTransactions()
    {
        // Arrange
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 100m,
            Label = "DEPENSE COURSES",
            Type = TransactionType.Debit,
            IsSplit = true
        };
        var s1 = new TransactionSplit { Id = Guid.NewGuid(), UserId = "test-user-id", TransactionId = tx.Id, Amount = 60m, CategoryId = _catFoodId };
        var s2 = new TransactionSplit { Id = Guid.NewGuid(), UserId = "test-user-id", TransactionId = tx.Id, Amount = 40m, CategoryId = _catHousingId };
        tx.Splits.Add(s1);
        tx.Splits.Add(s2);
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        var model = CreateDetailsModel();

        // Act
        var result = await model.OnGetAsync(_bankAccountId);

        // Assert
        result.Should().BeOfType<PageResult>();
        model.Transactions.Should().NotBeEmpty();
        var foundTx = model.Transactions.First(t => t.Id == tx.Id);
        foundTx.IsSplit.Should().BeTrue();
        foundTx.Splits.Should().HaveCount(2);
    }

    [Fact]
    public void RenderView_WhenSplitFeatureEnabled_ContainsSplitButtonsAndModals()
    {
        // Arrange & Act
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ledger.slnx")) && !File.Exists(Path.Combine(dir.FullName, "Ledger.sln")))
        {
            dir = dir.Parent;
        }

        var solutionDir = dir?.FullName;
        solutionDir.Should().NotBeNullOrEmpty("Solution root must be found");

        var detailsViewPath = Path.Combine(solutionDir!, "Ledger", "Features", "Accounts", "Pages", "Details.cshtml");
        var transactionsViewPath = Path.Combine(solutionDir!, "Ledger", "Features", "Accounts", "Pages", "Transactions.cshtml");

        File.Exists(detailsViewPath).Should().BeTrue("Details.cshtml must exist");
        File.Exists(transactionsViewPath).Should().BeTrue("Transactions.cshtml must exist");

        var detailsContent = File.ReadAllText(detailsViewPath);
        var transactionsContent = File.ReadAllText(transactionsViewPath);

        // Assert Details.cshtml
        detailsContent.Should().Contain("btn-split-tx", "Details.cshtml must contain split button with class btn-split-tx");
        detailsContent.Should().Contain("openSplitModal", "Details.cshtml must have openSplitModal handler");
        detailsContent.Should().Contain("bi-diagram-2", "Details.cshtml must render split diagram icon");
        detailsContent.Should().Contain("id=\"splitTransactionModal\"", "Details.cshtml must have splitTransactionModal");
        detailsContent.Should().Contain("SaveTransactionSplits", "Details.cshtml must call SaveTransactionSplits");
        detailsContent.Should().Contain("RemoveTransactionSplits", "Details.cshtml must call RemoveTransactionSplits");
        detailsContent.Should().Contain("Transactions.SplitPartPrefix", "Details.cshtml must render split part prefix");

        // Assert Transactions.cshtml
        transactionsContent.Should().Contain("btn-split-tx", "Transactions.cshtml must contain split button with class btn-split-tx");
        transactionsContent.Should().Contain("openSplitModal", "Transactions.cshtml must have openSplitModal handler");
        transactionsContent.Should().Contain("bi-diagram-2", "Transactions.cshtml must render split diagram icon");
        transactionsContent.Should().Contain("id=\"splitTransactionModal\"", "Transactions.cshtml must have splitTransactionModal");
        transactionsContent.Should().Contain("SaveTransactionSplits", "Transactions.cshtml must call SaveTransactionSplits");
        transactionsContent.Should().Contain("RemoveTransactionSplits", "Transactions.cshtml must call RemoveTransactionSplits");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
