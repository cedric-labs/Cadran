using FluentAssertions;
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

public class TransactionsUpdateCategoryTests : IDisposable
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
            _mockLocalizedEntityService.Object)
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
}
