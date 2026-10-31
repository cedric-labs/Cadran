using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Pages;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Merchants.Services;
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
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Ledger.Tests.Accounts;

public sealed class TransactionsCreateManualTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly ManualTransactionService _service;
    private readonly Guid _accountId = Guid.NewGuid();

    public TransactionsCreateManualTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque Test" };
        _context.Banks.Add(bank);
        _context.BankAccounts.Add(new BankAccount { Id = _accountId, UserId = "test-user-id", BankId = bank.Id, Name = "Compte Courant" });
        _context.SaveChanges();
        _service = new ManualTransactionService(_context, TimeProvider.System);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private TransactionsModel CreateModel()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());

        return new TransactionsModel(
            _context,
            Mock.Of<ITransactionStatusService>(),
            Mock.Of<IUiSettingsService>(),
            Mock.Of<IRulesEngineService>(),
            Mock.Of<IMerchantService>(),
            Mock.Of<IRecurringExpenseService>(),
            Mock.Of<ILocalizedEntityService>(),
            TimeProvider.System)
        {
            PageContext = new PageContext(actionContext),
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };
    }

    private TransactionsModel.ManualTransactionForm Form(string? label = "Boulangerie", string? amount = "12.50") => new()
    {
        BankAccountId = _accountId,
        Date = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Unspecified),
        Label = label,
        AmountText = amount,
        Type = TransactionType.Debit
    };

    [Fact]
    public async Task OnPostCreateTransactionAsync_WhenFormIsValid_CreatesManualTransactionAndRedirects()
    {
        var model = CreateModel();

        var result = await model.OnPostCreateTransactionAsync(_service, Form());

        result.Should().BeOfType<RedirectResult>();
        model.TempData["ManualEntryResult"].Should().Be("Created");
        var saved = await _context.Transactions.SingleAsync();
        saved.Origin.Should().Be(TransactionOrigin.Manual);
        saved.Amount.Should().Be(12.50m);
    }

    [Fact]
    public async Task OnPostCreateTransactionAsync_WhenAmountUsesCommaSeparator_ParsesAmount()
    {
        var model = CreateModel();

        await model.OnPostCreateTransactionAsync(_service, Form(amount: "1 234,56"));

        var saved = await _context.Transactions.SingleAsync();
        saved.Amount.Should().Be(1234.56m);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("-3")]
    public async Task OnPostCreateTransactionAsync_WhenAmountIsInvalid_ReportsInvalidAmountAndSavesNothing(string? amount)
    {
        var model = CreateModel();

        var result = await model.OnPostCreateTransactionAsync(_service, Form(amount: amount));

        result.Should().BeOfType<RedirectResult>();
        model.TempData["ManualEntryResult"].Should().Be("InvalidAmount");
        (await _context.Transactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OnPostCreateTransactionAsync_WhenLabelIsBlank_ReportsLabelRequiredAndSavesNothing()
    {
        var model = CreateModel();

        await model.OnPostCreateTransactionAsync(_service, Form(label: " "));

        model.TempData["ManualEntryResult"].Should().Be("LabelRequired");
        (await _context.Transactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OnPostCreateTransactionAsync_WhenAccountIsUnknown_ReportsAccountNotFound()
    {
        var model = CreateModel();
        var form = Form();
        form.BankAccountId = Guid.NewGuid();

        await model.OnPostCreateTransactionAsync(_service, form);

        model.TempData["ManualEntryResult"].Should().Be("AccountNotFound");
    }
}
