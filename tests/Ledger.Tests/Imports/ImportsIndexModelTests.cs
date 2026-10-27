using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Imports.Entities;
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
using Xunit;

namespace Ledger.Tests.Imports;

public class ImportsIndexModelTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly Mock<ITransactionImportService> _mockImportService;
    private readonly Mock<IAccountTypeService> _mockAccountTypeService;
    private readonly Mock<IUiSettingsService> _mockUiSettings;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly BankAccount _account;
    private readonly Bank _bank;

    public ImportsIndexModelTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();

        _bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Credit Agricole" };
        _context.Banks.Add(_bank);

        _account = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = _bank.Id,
            Name = "Compte Cheques",
            IsArchived = false
        };
        _context.BankAccounts.Add(_account);
        _context.SaveChanges();

        _mockImportService = new Mock<ITransactionImportService>();
        _mockAccountTypeService = new Mock<IAccountTypeService>();
        _mockAccountTypeService.Setup(s => s.GetAllAccountTypesAsync(default)).ReturnsAsync(new List<CustomAccountType>());

        _mockUiSettings = new Mock<IUiSettingsService>();
        _mockUiSettings.Setup(u => u.GetDuplicateToleranceDaysAsync()).ReturnsAsync(5);

        _mockLocalizer = new Mock<IStringLocalizer>();
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));

        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();
        _mockLocalizedEntityService
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
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

    private static FormFile CreateMockFormFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "File", fileName);
    }

    [Fact]
    public async Task OnGetAsync_WhenCalled_LoadsAccountsAndTolerance()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnGetAsync(_account.Id);

        result.Should().BeOfType<PageResult>();
        model.Input.BankAccountId.Should().Be(_account.Id);
        model.AvailableAccounts.Should().ContainSingle(a => a.Name == "Compte Cheques");
        model.ConfiguredToleranceDays.Should().Be(5);
    }

    [Fact]
    public async Task OnPostAsync_WhenNoFile_ShouldAddErrorAndReturnPage()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Input = new IndexModel.UploadInput { File = null }
        };

        var result = await model.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        model.ModelState.ContainsKey("Input.File").Should().BeTrue();
    }

    [Fact]
    public async Task OnPostAsync_WhenCsvAndNoAccountSelected_ShouldAddErrorAndReturnPage()
    {
        var file = CreateMockFormFile("statement.csv", "Date;Amount\n2026-10-01;50");
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Input = new IndexModel.UploadInput
            {
                File = file,
                BankAccountId = null
            }
        };

        var result = await model.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        model.ModelState.ContainsKey("Input.BankAccountId").Should().BeTrue();
    }

    [Fact]
    public async Task OnPostAsync_WhenValidCsv_ShouldCallImportServiceAndReturnPage()
    {
        var file = CreateMockFormFile("statement.csv", "Date;Amount\n2026-10-01;50");
        var importResult = new ImportResultDto(
            Success: true,
            Message: "Import réussi",
            TotalParsed: 1,
            TotalImported: 1,
            TotalDuplicatesSkipped: 0,
            TotalAutoCategorized: 0,
            BatchId: Guid.NewGuid());

        _mockImportService.Setup(s => s.ImportStatementAsync(
            _account.Id,
            "statement.csv",
            It.IsAny<Stream>(),
            null,
            default)).ReturnsAsync(importResult);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData,
            Input = new IndexModel.UploadInput
            {
                File = file,
                BankAccountId = _account.Id
            }
        };

        var result = await model.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        model.LastResult.Should().NotBeNull();
        model.LastResult!.TotalImported.Should().Be(1);
    }

    [Fact]
    public async Task OnPostProcessPendingOfxAsync_WhenTempFileMissing_ShouldRedirect()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostProcessPendingOfxAsync(
            tempFilePath: "C:\\non_existent_file.ofx",
            originalFileName: "statement.ofx",
            detectedOfxAcctId: "12345",
            chosenAccountId: _account.Id,
            associateOfxIdToAccount: false);

        result.Should().BeOfType<RedirectToPageResult>();
        model.StatusNotification.Should().NotBeNull();
    }

    [Fact]
    public async Task OnPostProcessPendingOfxAsync_WhenAccountSelected_ShouldImportAndDeleteTempFile()
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "OFXHEADER");

        var importResult = new ImportResultDto(
            Success: true,
            Message: "Import OFX réussi",
            TotalParsed: 2,
            TotalImported: 2,
            TotalDuplicatesSkipped: 0,
            TotalAutoCategorized: 0,
            BatchId: Guid.NewGuid());

        _mockImportService.Setup(s => s.ImportStatementAsync(
            _account.Id,
            "statement.ofx",
            It.IsAny<Stream>(),
            null,
            default)).ReturnsAsync(importResult);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostProcessPendingOfxAsync(
            tempFilePath: tempFile,
            originalFileName: "statement.ofx",
            detectedOfxAcctId: "987654321",
            chosenAccountId: _account.Id,
            associateOfxIdToAccount: true);

        result.Should().BeOfType<PageResult>();
        model.LastResult.Should().NotBeNull();
        model.LastResult!.TotalImported.Should().Be(2);

        // Verify account had its OfxAccountId updated
        var updatedAccount = await _context.BankAccounts.FindAsync(_account.Id);
        updatedAccount!.OfxAccountId.Should().Be("987654321");

        // Verify temp file was cleaned up
        File.Exists(tempFile).Should().BeFalse();
    }

    [Fact]
    public async Task OnPostConfirmDuplicatesAsync_WithValidSelection_ShouldCallConfirmDuplicates()
    {
        var batchId = Guid.NewGuid();
        var duplicates = new List<SuspectedDuplicateDto>
        {
            new(Index: 0, Date: DateTime.Today, ValueDate: null, Amount: 42m, Type: TransactionType.Debit, Label: "Test", RawLabel: null, ExistingMatchLabel: null),
            new(Index: 1, Date: DateTime.Today, ValueDate: null, Amount: 10m, Type: TransactionType.Debit, Label: "Other", RawLabel: null, ExistingMatchLabel: null)
        };
        var serialized = JsonSerializer.Serialize(duplicates);

        _mockImportService.Setup(s => s.ConfirmDuplicatesAsync(
            batchId,
            _account.Id,
            It.Is<IReadOnlyList<SuspectedDuplicateDto>>(l => l.Count == 1),
            default)).ReturnsAsync(1);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostConfirmDuplicatesAsync(
            batchId,
            _account.Id,
            serialized,
            new List<int> { 0 });

        result.Should().BeOfType<PageResult>();
        _mockImportService.Verify(s => s.ConfirmDuplicatesAsync(batchId, _account.Id, It.IsAny<IReadOnlyList<SuspectedDuplicateDto>>(), default), Times.Once);
    }

    [Fact]
    public async Task OnPostFindDuplicatesAsync_WhenCalled_PopulatesExistingDuplicates()
    {
        _mockImportService.Setup(s => s.FindExistingDuplicatesAsync(_account.Id, default))
            .ReturnsAsync(new List<ExistingDuplicateGroupDto>());

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostFindDuplicatesAsync(_account.Id);

        result.Should().BeOfType<PageResult>();
        model.SelectedDeduplicateAccountId.Should().Be(_account.Id);
        model.ExistingDuplicates.Should().NotBeNull();
    }

    [Fact]
    public async Task OnPostDeleteDuplicatesAsync_WithValidIds_ShouldCallDelete()
    {
        var txIds = new List<Guid> { Guid.NewGuid() };
        _mockImportService.Setup(s => s.DeleteExistingDuplicatesAsync(_account.Id, txIds, default)).ReturnsAsync(1);
        _mockImportService.Setup(s => s.FindExistingDuplicatesAsync(_account.Id, default)).ReturnsAsync(new List<ExistingDuplicateGroupDto>());

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostDeleteDuplicatesAsync(_account.Id, txIds);

        result.Should().BeOfType<PageResult>();
        _mockImportService.Verify(s => s.DeleteExistingDuplicatesAsync(_account.Id, txIds, default), Times.Once);
    }

    [Fact]
    public async Task OnPostDeleteBatchAsync_WhenCalled_DeletesBatchAndRedirects()
    {
        var batchId = Guid.NewGuid();
        _mockImportService.Setup(s => s.DeleteBatchAsync(batchId, default)).ReturnsAsync(true);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostDeleteBatchAsync(batchId);

        result.Should().BeOfType<PageResult>();
        _mockImportService.Verify(s => s.DeleteBatchAsync(batchId, default), Times.Once);
    }

    [Fact]
    public async Task OnPostCreateAccountAndImportAsync_ValidationBranches_ReturnRedirect()
    {
        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        // 1. Missing temp file
        var res1 = await model.OnPostCreateAccountAndImportAsync("non_existent_file.csv", "test.csv", null, null, null, "Compte", null, 0m);
        res1.Should().BeOfType<RedirectToPageResult>();

        // 2. Empty account name
        var tempFile = Path.GetTempFileName();
        try
        {
            var res2 = await model.OnPostCreateAccountAndImportAsync(tempFile, "test.csv", null, null, null, "  ", null, 0m);
            res2.Should().BeOfType<RedirectToPageResult>();

            // 3. Missing bank
            var res3 = await model.OnPostCreateAccountAndImportAsync(tempFile, "test.csv", null, null, "   ", "Mon Compte", null, 0m);
            res3.Should().BeOfType<RedirectToPageResult>();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task OnPostCreateAccountAndImportAsync_Valid_CreatesBankAndAccountAndImports()
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "01/01/2026;Test;50");

        var customTypeId = Guid.NewGuid();
        var customType = new CustomAccountType { Id = customTypeId, UserId = "test-user-id", Name = "Epargne", Code = "Savings" };
        _context.CustomAccountTypes.Add(customType);
        await _context.SaveChangesAsync();

        _mockAccountTypeService.Setup(s => s.GetByIdAsync(customTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customType);

        var importResult = new ImportResultDto(
            Success: true,
            Message: "Import réussi",
            TotalParsed: 5,
            TotalImported: 5,
            TotalDuplicatesSkipped: 0,
            TotalAutoCategorized: 0,
            BatchId: Guid.NewGuid());

        _mockImportService.Setup(s => s.ImportStatementAsync(It.IsAny<Guid>(), "test.csv", It.IsAny<Stream>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        var (pageContext, tempData) = CreateContext();
        var model = new IndexModel(
            _context,
            _mockImportService.Object,
            _mockAccountTypeService.Object,
            _mockUiSettings.Object,
            _mockLocalizer.Object,
            _mockLocalizedEntityService.Object)
        {
            PageContext = pageContext,
            TempData = tempData
        };

        var result = await model.OnPostCreateAccountAndImportAsync(
            tempFile,
            "test.csv",
            "OFX12345",
            null,
            "Banque Populaire",
            "Nouveau Livret",
            customTypeId,
            250m);

        result.Should().BeOfType<PageResult>();
        model.LastResult.Should().NotBeNull();
        model.LastResult!.TotalImported.Should().Be(5);

        var createdAccount = _context.BankAccounts.FirstOrDefault(a => a.Name == "Nouveau Livret");
        createdAccount.Should().NotBeNull();
        createdAccount!.Type.Should().Be(AccountType.Savings);
        createdAccount.InitialBalance.Should().Be(250m);
        createdAccount.OfxAccountId.Should().Be("OFX12345");
    }
}
