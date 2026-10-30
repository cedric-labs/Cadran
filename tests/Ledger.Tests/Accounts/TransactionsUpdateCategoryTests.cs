using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Pages;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Merchants.Entities;
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

public sealed class TransactionsUpdateCategoryTests : IDisposable
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

    public TransactionsUpdateCategoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _mockStatusService = new Mock<ITransactionStatusService>();
        _mockUiSettingsService = new Mock<IUiSettingsService>();
        _mockRulesEngineService = new Mock<IRulesEngineService>();
        _mockMerchantService = new Mock<IMerchantService>();
        _mockRecurringService = new Mock<IRecurringExpenseService>();
        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();

        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque A" };
        var account = new BankAccount { Id = _bankAccountId, UserId = "test-user-id", BankId = bank.Id, Name = "Compte Courant" };
        _context.Banks.Add(bank);
        _context.BankAccounts.Add(account);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task OnPostUpdateCategoryAsync_WithNewMerchantName_ShouldCreateMerchantAndAssign()
    {
        // Arrange
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 45m,
            Label = "LIDL LENS 62",
            Type = TransactionType.Debit
        };
        _context.Transactions.Add(tx);
        await _context.SaveChangesAsync();

        var createdMerchant = new Merchant
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Lidl",
            NormalizedName = "lidl"
        };
        _context.Merchants.Add(createdMerchant);
        await _context.SaveChangesAsync();

        _mockMerchantService
            .Setup(m => m.GetOrCreateByNameAsync("Lidl", null, default))
            .ReturnsAsync(createdMerchant);
        _mockMerchantService
            .Setup(m => m.GetByIdAsync(createdMerchant.Id, default))
            .ReturnsAsync(createdMerchant);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var model = new TransactionsModel(
            _context,
            _mockStatusService.Object,
            _mockUiSettingsService.Object,
            _mockRulesEngineService.Object,
            _mockMerchantService.Object,
            _mockRecurringService.Object,
            _mockLocalizedEntityService.Object,
            TimeProvider.System)
        {
            PageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary()))
        };

        // Act
        var result = await model.OnPostUpdateCategoryAsync(
            transactionId: tx.Id,
            newCategoryId: null,
            merchantId: null,
            newMerchantName: "Lidl");

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        jsonResult.Value.Should().NotBeNull();

        var updatedTx = await _context.Transactions.FindAsync(tx.Id);
        updatedTx!.MerchantId.Should().Be(createdMerchant.Id);
    }

    private TransactionsModel CreateJsonModel()
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
            _mockLocalizedEntityService.Object,
            TimeProvider.System)
        {
            PageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary()))
        };
    }

    private async Task<Transaction> AddTransactionAsync(string label, Guid? categoryId = null)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            Amount = 12m,
            Label = label,
            Type = TransactionType.Debit,
            CategoryId = categoryId
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    private static System.Text.Json.JsonElement ToJson(IActionResult result)
    {
        var json = result.Should().BeOfType<JsonResult>().Subject;
        return System.Text.Json.JsonSerializer.SerializeToElement(json.Value);
    }

    [Fact]
    public async Task OnPostUpdateCategoryAsync_WhenCategoryAndShiftedBudgetDateGiven_ReturnsCategoryAndBudgetPayload()
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Courses", IconName = "bi-cart", ColorHex = "#123456" };
        _context.Categories.Add(category);
        var tx = await AddTransactionAsync("MARCHE");
        var budgetDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await CreateJsonModel().OnPostUpdateCategoryAsync(tx.Id, category.Id, budgetDate, "  note  ");

        // Assert
        var payload = ToJson(result);
        payload.GetProperty("success").GetBoolean().Should().BeTrue();
        payload.GetProperty("categoryId").GetGuid().Should().Be(category.Id);
        payload.GetProperty("categoryName").GetString().Should().Be("Courses");
        payload.GetProperty("categoryIcon").GetString().Should().Be("bi-cart");
        payload.GetProperty("categoryColor").GetString().Should().Be("#123456");
        payload.GetProperty("budgetDate").GetString().Should().Be("2026-04-01");
        payload.GetProperty("isShifted").GetBoolean().Should().BeTrue();
        payload.GetProperty("budgetBadgeText").GetString().Should().Be(budgetDate.ToString("MM/yyyy", System.Globalization.CultureInfo.CurrentCulture));
        payload.GetProperty("notes").GetString().Should().Be("note");
        payload.GetProperty("merchantName").GetString().Should().BeEmpty();
    }

    [Fact]
    public async Task OnPostUpdateCategoryAsync_WhenNoCategoryAndSameMonthBudgetDate_ReturnsDefaultsWithoutShift()
    {
        // Arrange
        var tx = await AddTransactionAsync("DIVERS");

        // Act
        var result = await CreateJsonModel().OnPostUpdateCategoryAsync(
            tx.Id, Guid.Empty, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), "   ");

        // Assert
        var payload = ToJson(result);
        payload.GetProperty("categoryName").GetString().Should().Be("Non catégorisé");
        payload.GetProperty("categoryIcon").GetString().Should().Be("bi-tag");
        payload.GetProperty("categoryColor").GetString().Should().Be("#6c757d");
        payload.GetProperty("isShifted").GetBoolean().Should().BeFalse();
        payload.GetProperty("budgetBadgeText").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        payload.GetProperty("notes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        (await _context.Transactions.FindAsync(tx.Id))!.CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task OnPostUpdateCategoryAsync_WhenApplyToSimilarLabels_UpdatesOnlyTransactionsWithSameLabel()
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Loisirs" };
        _context.Categories.Add(category);
        var tx = await AddTransactionAsync("CINEMA");
        var twin = await AddTransactionAsync("CINEMA");
        var other = await AddTransactionAsync("BOULANGER");

        // Act
        await CreateJsonModel().OnPostUpdateCategoryAsync(tx.Id, category.Id, null, "vu", applyToSimilarLabels: true);

        // Assert
        (await _context.Transactions.FindAsync(twin.Id))!.CategoryId.Should().Be(category.Id);
        (await _context.Transactions.FindAsync(tx.Id))!.CategoryId.Should().Be(category.Id);
        (await _context.Transactions.FindAsync(tx.Id))!.Notes.Should().Be("vu");
        (await _context.Transactions.FindAsync(twin.Id))!.Notes.Should().BeNull();
        (await _context.Transactions.FindAsync(other.Id))!.CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task OnPostUpdateCategoryAsync_WhenNotApplyingToSimilarLabels_UpdatesOnlyTheTransaction()
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Loisirs" };
        _context.Categories.Add(category);
        var tx = await AddTransactionAsync("CINEMA");
        var twin = await AddTransactionAsync("CINEMA");

        // Act
        await CreateJsonModel().OnPostUpdateCategoryAsync(tx.Id, category.Id, null);

        // Assert
        (await _context.Transactions.FindAsync(tx.Id))!.CategoryId.Should().Be(category.Id);
        (await _context.Transactions.FindAsync(twin.Id))!.CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task OnPostUpdateCategoryAsync_WhenCustomStatusHasSystemCode_SetsCustomStatusAndEnumStatus()
    {
        // Arrange
        var customStatus = new TransactionCustomStatus { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Pointée banque", Code = "Reconciled" };
        _context.TransactionStatuses.Add(customStatus);
        await _context.SaveChangesAsync();
        _mockStatusService.Setup(s => s.GetByIdAsync(customStatus.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customStatus);
        var tx = await AddTransactionAsync("LOYER");

        // Act
        var result = await CreateJsonModel().OnPostUpdateCategoryAsync(tx.Id, null, null, statusId: customStatus.Id, status: TransactionStatus.Cleared);

        // Assert
        var saved = await _context.Transactions.FindAsync(tx.Id);
        saved!.CustomStatusId.Should().Be(customStatus.Id);
        saved.Status.Should().Be(TransactionStatus.Reconciled);
        ToJson(result).GetProperty("statusDisplayName").GetString().Should().Be("Pointée banque");
    }

    [Fact]
    public async Task OnPostUpdateCategoryAsync_WhenMerchantIdIsEmptyGuid_ClearsMerchant()
    {
        // Arrange
        var merchant = new Merchant { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Fnac", NormalizedName = "fnac" };
        _context.Merchants.Add(merchant);
        var tx = await AddTransactionAsync("FNAC");
        tx.MerchantId = merchant.Id;
        await _context.SaveChangesAsync();

        // Act
        await CreateJsonModel().OnPostUpdateCategoryAsync(tx.Id, null, null, merchantId: Guid.Empty);

        // Assert
        (await _context.Transactions.FindAsync(tx.Id))!.MerchantId.Should().BeNull();
    }
}
