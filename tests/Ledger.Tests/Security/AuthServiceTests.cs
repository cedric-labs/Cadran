using System.Text.Encodings.Web;
using AwesomeAssertions;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Services;
using Ledger.Features.Security.Models;
using Ledger.Features.Security.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ledger.Tests.Security;

public class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<ICategorySeedService> _mockCategorySeedService;
    private readonly Mock<IAccountTypeService> _mockAccountTypeService;
    private readonly Mock<ITransactionStatusService> _mockTransactionStatusService;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object,
            new IdentityErrorDescriber(),
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
            _mockUserManager.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<ApplicationUser>>().Object);

        _mockCategorySeedService = new Mock<ICategorySeedService>();
        _mockAccountTypeService = new Mock<IAccountTypeService>();
        _mockTransactionStatusService = new Mock<ITransactionStatusService>();

        _service = new AuthService(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            UrlEncoder.Default,
            _mockCategorySeedService.Object,
            _mockAccountTypeService.Object,
            _mockTransactionStatusService.Object,
            TimeProvider.System);
    }

    [Fact]
    public async Task RegisterUserAsync_WhenCreateSucceeds_ShouldSeedUserDataAndReturnSuccess()
    {
        _mockUserManager
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.RegisterUserAsync("alice@Ledger.local", "Password123!", "Alice Test");

        result.Succeeded.Should().BeTrue();
        _mockCategorySeedService.Verify(c => c.EnsureDefaultCategoriesForUserAsync(It.IsAny<string>(), default), Times.Once);
        _mockAccountTypeService.Verify(a => a.EnsureDefaultAccountTypesForUserAsync(It.IsAny<string>(), default), Times.Once);
        _mockTransactionStatusService.Verify(s => s.EnsureDefaultStatusesForUserAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RegisterUserAsync_WhenCreateFails_ShouldNotSeedUserData()
    {
        _mockUserManager
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var result = await _service.RegisterUserAsync("alice@Ledger.local", "weak", "Alice Test");

        result.Succeeded.Should().BeFalse();
        _mockCategorySeedService.Verify(c => c.EnsureDefaultCategoriesForUserAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task LoginUserAsync_WhenUserNotFound_ShouldReturnFailed()
    {
        _mockUserManager
            .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _service.LoginUserAsync("nonexistent@Ledger.local", "pwd", false);

        result.Should().Be(SignInResult.Failed);
    }

    [Fact]
    public async Task LoginUserAsync_WhenUserFoundAndLoginSucceeds_ShouldUpdateLastLogin()
    {
        var user = new ApplicationUser { UserName = "alice", Email = "alice@Ledger.local" };
        _mockUserManager
            .Setup(m => m.FindByEmailAsync("alice@Ledger.local"))
            .ReturnsAsync(user);

        _mockSignInManager
            .Setup(s => s.PasswordSignInAsync("alice", "ValidPassword!", false, true))
            .ReturnsAsync(SignInResult.Success);

        _mockUserManager
            .Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.LoginUserAsync("alice@Ledger.local", "ValidPassword!", false);

        result.Should().Be(SignInResult.Success);
        user.LastLoginAtUtc.Should().NotBeNull();
        _mockUserManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task GenerateMfaSetupAsync_WhenUserProvided_GeneratesKeyAndQrCode()
    {
        var user = new ApplicationUser { UserName = "alice", Email = "alice@Ledger.local" };
        _mockUserManager
            .Setup(m => m.GetAuthenticatorKeyAsync(user))
            .ReturnsAsync("ABCDEF123456");

        var setup = await _service.GenerateMfaSetupAsync(user);

        setup.SharedKey.Should().NotBeNullOrEmpty();
        setup.AuthenticatorUri.Should().Contain("otpauth://totp/");
        setup.QrCodeBase64.Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public async Task SignOutAsync_WhenCalled_CallsSignInManagerSignOut()
    {
        await _service.SignOutAsync();
        _mockSignInManager.Verify(s => s.SignOutAsync(), Times.Once);
    }

    [Fact]
    public async Task LoginUserAsync_WhenLoginSucceeds_SetsLastLoginAtUtcFromTimeProvider()
    {
        var fixedUtc = new DateTimeOffset(2025, 3, 15, 9, 15, 0, TimeSpan.Zero);
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(fixedUtc);
        var service = new AuthService(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            UrlEncoder.Default,
            _mockCategorySeedService.Object,
            _mockAccountTypeService.Object,
            _mockTransactionStatusService.Object,
            fakeTime);

        var user = new ApplicationUser { UserName = "alice", Email = "alice@Ledger.local" };
        _mockUserManager.Setup(m => m.FindByEmailAsync("alice@Ledger.local")).ReturnsAsync(user);
        _mockSignInManager.Setup(s => s.PasswordSignInAsync("alice", "ValidPassword!", false, true)).ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await service.LoginUserAsync("alice@Ledger.local", "ValidPassword!", false);

        result.Should().Be(SignInResult.Success);
        user.LastLoginAtUtc.Should().Be(fixedUtc.UtcDateTime);
    }
}
