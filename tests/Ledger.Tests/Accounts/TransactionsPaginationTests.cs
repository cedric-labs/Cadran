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
using Moq;
using Xunit;

namespace Ledger.Tests.Accounts;

public sealed class TransactionsPaginationTests : IDisposable
{
    private readonly Ledger.Infrastructure.Data.AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Mock<ITransactionStatusService> _mockStatusService;
    private readonly Mock<IUiSettingsService> _mockUiSettingsService;
    private readonly Mock<IRulesEngineService> _mockRulesEngineService;
    private readonly Mock<IMerchantService> _mockMerchantService;
    private readonly Mock<IRecurringExpenseService> _mockRecurringService;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;

    public TransactionsPaginationTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _mockStatusService = new Mock<ITransactionStatusService>();
        _mockUiSettingsService = new Mock<IUiSettingsService>();
        _mockRulesEngineService = new Mock<IRulesEngineService>();
        _mockMerchantService = new Mock<IMerchantService>();
        _mockRecurringService = new Mock<IRecurringExpenseService>();
        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();

        _mockUiSettingsService.Setup(s => s.GetDefaultPeriodAsync()).ReturnsAsync("all");
        _mockUiSettingsService.Setup(s => s.GetTransactionsPageSizeAsync()).ReturnsAsync(50);
        _mockUiSettingsService.Setup(s => s.GetShowKpiCentsAsync()).ReturnsAsync(true);
        _mockStatusService.Setup(s => s.GetAllStatusesAsync()).ReturnsAsync(new List<TransactionCustomStatus>());
        _mockMerchantService.Setup(s => s.GetAllMerchantsAsync(default)).ReturnsAsync(new List<Merchant>());
    }

    [Fact]
    public void ComputePagination_WhenInvoked_ComputesPropertiesCorrectly()
    {
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
            TotalCount = 125,
            EffectivePageSize = 50,
            PageNumber = 2
        };

        model.TotalPages.Should().Be(3);
        model.HasPreviousPage.Should().BeTrue();
        model.HasNextPage.Should().BeTrue();
        model.StartItemIndex.Should().Be(51);
        model.EndItemIndex.Should().Be(100);
    }

    [Fact]
    public void PaginationProperties_FirstAndLastPages_ShouldSetFlagsCorrectly()
    {
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
            TotalCount = 100,
            EffectivePageSize = 50,
            PageNumber = 1
        };

        model.HasPreviousPage.Should().BeFalse();
        model.HasNextPage.Should().BeTrue();

        model.PageNumber = 2;
        model.HasPreviousPage.Should().BeTrue();
        model.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void GetPageUrl_WhenCalled_PreservesQueryParametersAndSetsPage()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?SearchTerm=test&Type=Debit&PageNumber=1");

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
            PageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary())),
            EffectivePageSize = 25
        };

        var url = model.GetPageUrl(3);

        url.Should().Contain("PageNumber=3");
        url.Should().Contain("PageSize=25");
        url.Should().Contain("SearchTerm=test");
        url.Should().Contain("Type=Debit");
    }

    [Fact]
    public async Task OnGetAsync_WhenMultiplePagesExist_PaginatesTransactions()
    {
        // Arrange: créer un compte et 75 transactions
        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque A" };
        var account = new BankAccount { Id = Guid.NewGuid(), UserId = "test-user-id", BankId = bank.Id, Name = "Compte A" };
        _context.Banks.Add(bank);
        _context.BankAccounts.Add(account);

        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 75; i++)
        {
            _context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = baseDate.AddDays(i),
                Amount = 10m + i,
                Label = $"Transaction {i}",
                Type = TransactionType.Debit
            });
        }
        await _context.SaveChangesAsync();

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
            Period = "all",
            PageSize = 25,
            PageNumber = 2
        };

        // Act
        await model.OnGetAsync();

        // Assert
        model.TotalCount.Should().Be(75);
        model.TotalPages.Should().Be(3);
        model.Transactions.Should().HaveCount(25);
        model.PageNumber.Should().Be(2);
        model.StartItemIndex.Should().Be(26);
        model.EndItemIndex.Should().Be(50);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
