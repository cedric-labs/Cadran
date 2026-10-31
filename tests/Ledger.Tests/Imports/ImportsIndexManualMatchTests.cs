using System.Text;
using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Imports.Pages;
using Ledger.Features.Imports.Services;
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
using Microsoft.Extensions.Localization;
using Moq;

namespace Ledger.Tests.Imports;

public sealed class ImportsIndexManualMatchTests : IDisposable
{
    private static readonly DateTime Day = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<ITransactionImportService> _importService = new();
    private readonly Mock<IUiSettingsService> _settings = new();
    private readonly Guid _accountId = Guid.NewGuid();

    public ImportsIndexManualMatchTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque Test" };
        _context.Banks.Add(bank);
        _context.BankAccounts.Add(new BankAccount { Id = _accountId, UserId = "test-user-id", BankId = bank.Id, Name = "Compte Courant" });
        _context.SaveChanges();

        _settings.Setup(u => u.GetDuplicateToleranceDaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void AddPair()
    {
        foreach (var origin in new[] { TransactionOrigin.Manual, TransactionOrigin.Import })
        {
            _context.Transactions.Add(new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = _accountId,
                Date = Day,
                Amount = 25m,
                Type = TransactionType.Debit,
                Label = "Courses",
                Origin = origin,
            });
        }

        _context.SaveChanges();
    }

    private async Task<IndexModel> PostImportAsync(bool success)
    {
        var bytes = Encoding.UTF8.GetBytes("Date;Amount\n2026-10-20;25");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "statement.csv");
        _importService
            .Setup(s => s.ImportStatementAsync(_accountId, "statement.csv", It.IsAny<Stream>(), null, default))
            .ReturnsAsync(new ImportResultDto(success, "msg", 1, 1, 0, 0, Guid.NewGuid()));

        var accountTypes = new Mock<IAccountTypeService>();
        accountTypes.Setup(s => s.GetAllAccountTypesAsync(default)).ReturnsAsync([]);
        var localizer = new Mock<IStringLocalizer>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));
        var localized = new Mock<ILocalizedEntityService>();
        localized
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        var model = new IndexModel(
            _context,
            _importService.Object,
            accountTypes.Object,
            _settings.Object,
            localizer.Object,
            localized.Object,
            matchLoader: new ManualEntryMatchLoader(_context, _settings.Object))
        {
            PageContext = new PageContext(actionContext),
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
            Input = new IndexModel.UploadInput { File = file, BankAccountId = _accountId },
        };

        await model.OnPostAsync();
        return model;
    }

    [Fact]
    public async Task OnPostAsync_WhenImportSucceedsAndAManualEntryMatches_CountsThePendingPair()
    {
        AddPair();

        var model = await PostImportAsync(success: true);

        model.PendingManualMatchCount.Should().Be(1);
    }

    [Fact]
    public async Task OnPostAsync_WhenNoManualEntryMatches_CountsNoPair()
    {
        var model = await PostImportAsync(success: true);

        model.PendingManualMatchCount.Should().Be(0);
    }

    [Fact]
    public async Task OnPostAsync_WhenImportFails_CountsNoPair()
    {
        AddPair();

        var model = await PostImportAsync(success: false);

        model.PendingManualMatchCount.Should().Be(0);
    }
}
