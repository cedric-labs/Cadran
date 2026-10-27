using AwesomeAssertions;
using Ledger.Features.Security.Pages;
using Ledger.Features.Security.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;
using Xunit;

namespace Ledger.Tests.Security;

public class UnlockDatabasePageTests
{
    private readonly Mock<IDatabaseEncryptionService> _mockService;
    private readonly UnlockDatabaseModel _pageModel;

    public UnlockDatabasePageTests()
    {
        _mockService = new Mock<IDatabaseEncryptionService>();
        _pageModel = new UnlockDatabaseModel(_mockService.Object);
    }

    [Fact]
    public void OnGet_WhenNotLocked_RedirectsToAccounts()
    {
        _mockService.Setup(s => s.IsDatabaseLocked()).Returns(false);

        var result = _pageModel.OnGet();

        result.Should().BeOfType<LocalRedirectResult>()
            .Which.Url.Should().Be("~/Accounts");
    }

    [Fact]
    public void OnGet_WhenLocked_ReturnsPage()
    {
        _mockService.Setup(s => s.IsDatabaseLocked()).Returns(true);

        var result = _pageModel.OnGet();

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnPostAsync_WithEmptyPassphrase_ReturnsPageWithError()
    {
        _pageModel.Passphrase = "";

        var result = await _pageModel.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        _pageModel.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task OnPostAsync_WithInvalidPassphrase_ReturnsPageWithError()
    {
        _pageModel.Passphrase = "WrongPassword123";
        _mockService.Setup(s => s.UnlockDatabaseAsync("WrongPassword123", true))
            .ReturnsAsync(false);

        var result = await _pageModel.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        _pageModel.ErrorMessage.Should().Contain("Phrase de passe incorrecte");
    }

    [Fact]
    public async Task OnPostAsync_WithValidPassphrase_RedirectsToAccounts()
    {
        _pageModel.Passphrase = "CorrectPass123";
        _mockService.Setup(s => s.UnlockDatabaseAsync("CorrectPass123", true))
            .ReturnsAsync(true);

        var result = await _pageModel.OnPostAsync();

        result.Should().BeOfType<LocalRedirectResult>()
            .Which.Url.Should().Be("~/Accounts");
    }
}

public class DatabaseEncryptionPageTests
{
    private readonly Mock<IDatabaseEncryptionService> _mockService;
    private readonly DatabaseEncryptionModel _pageModel;

    public DatabaseEncryptionPageTests()
    {
        _mockService = new Mock<IDatabaseEncryptionService>();
        _pageModel = new DatabaseEncryptionModel(_mockService.Object);
    }

    [Fact]
    public async Task OnGetAsync_WhenInvoked_PopulatesProperties()
    {
        _mockService.Setup(s => s.IsDatabaseEncrypted()).Returns(true);
        _mockService.Setup(s => s.IsRememberOnDeviceEnabledAsync()).ReturnsAsync(true);

        var result = await _pageModel.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        _pageModel.IsEncrypted.Should().BeTrue();
        _pageModel.IsRememberOnDevice.Should().BeTrue();
    }

    [Fact]
    public async Task OnPostEncryptAsync_WithMismatchConfirmation_SetsError()
    {
        _pageModel.EncryptInput = new DatabaseEncryptionModel.EncryptInputModel
        {
            NewPassphrase = "Password123456",
            ConfirmPassphrase = "MismatchPassword"
        };

        var result = await _pageModel.OnPostEncryptAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _pageModel.ErrorMessage.Should().Contain("confirmation");
    }

    [Fact]
    public async Task OnPostEncryptAsync_WithValidInputs_CallsService()
    {
        _pageModel.EncryptInput = new DatabaseEncryptionModel.EncryptInputModel
        {
            NewPassphrase = "ValidPassword123!",
            ConfirmPassphrase = "ValidPassword123!",
            RememberOnDevice = true
        };

        _mockService.Setup(s => s.EncryptDatabaseAsync("ValidPassword123!", true))
            .ReturnsAsync((true, null));

        var result = await _pageModel.OnPostEncryptAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _pageModel.StatusMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task OnPostChangePassphraseAsync_WithValidInputs_CallsService()
    {
        _pageModel.ChangePassphraseInput = new DatabaseEncryptionModel.ChangePassphraseInputModel
        {
            CurrentPassphrase = "OldPassword123!",
            NewPassphrase = "NewPassword123!",
            ConfirmPassphrase = "NewPassword123!",
            RememberOnDevice = true
        };

        _mockService.Setup(s => s.ChangePassphraseAsync("OldPassword123!", "NewPassword123!", true))
            .ReturnsAsync((true, null));

        var result = await _pageModel.OnPostChangePassphraseAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _pageModel.StatusMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task OnPostDecryptAsync_WithValidCurrentPassphrase_CallsService()
    {
        _pageModel.DecryptInput = new DatabaseEncryptionModel.DecryptInputModel
        {
            CurrentPassphrase = "CurrentPass123!"
        };

        _mockService.Setup(s => s.DecryptDatabaseToPlainAsync("CurrentPass123!"))
            .ReturnsAsync((true, null));

        var result = await _pageModel.OnPostDecryptAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        _pageModel.StatusMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task OnPostToggleRememberAsync_WhenInvoked_UpdatesRememberStatus()
    {
        var result = await _pageModel.OnPostToggleRememberAsync(true);

        result.Should().BeOfType<RedirectToPageResult>();
        _mockService.Verify(s => s.SetRememberOnDeviceAsync(true), Times.Once);
    }
}
