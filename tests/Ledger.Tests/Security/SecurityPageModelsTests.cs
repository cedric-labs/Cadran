using System.Security.Claims;
using AwesomeAssertions;
using Ledger.Features.Security.Models;
using Ledger.Features.Security.Pages;
using Ledger.Features.Security.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace Ledger.Tests.Security;

public class SecurityPageModelsTests
{
    private static readonly string[] MockRecoveryCodes = ["REC-1", "REC-2"];

    private static PageContext CreatePageContext()
    {
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new PageActionDescriptor(), modelState);
        return new PageContext(actionContext);
    }

    #region LoginModel Tests

    [Fact]
    public void OnGetLogin_WhenErrorMessageProvided_SetsReturnUrlAndAddsError()
    {
        var mockAuth = new Mock<IAuthService>();
        var model = new LoginModel(mockAuth.Object)
        {
            PageContext = CreatePageContext(),
            ErrorMessage = "Erreur précédente"
        };

        model.OnGet("/custom-return");

        model.ReturnUrl.Should().Be("/custom-return");
        model.ModelState.IsValid.Should().BeFalse();
        model.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage == "Erreur précédente");
    }

    [Fact]
    public async Task OnPostLoginAsync_WhenModelStateIsInvalid_ReturnsPage()
    {
        var mockAuth = new Mock<IAuthService>();
        var model = new LoginModel(mockAuth.Object)
        {
            PageContext = CreatePageContext()
        };
        model.ModelState.AddModelError("Email", "Email requis");

        var result = await model.OnPostAsync("/Accounts");

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnPostLoginAsync_WhenCredentialsValid_ReturnsLocalRedirect()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(a => a.LoginUserAsync("test@Ledger.local", "Password123!", true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var model = new LoginModel(mockAuth.Object)
        {
            PageContext = CreatePageContext(),
            Url = new Mock<IUrlHelper>().Object,
            Input = new LoginModel.InputModel
            {
                Email = "test@Ledger.local",
                Password = "Password123!",
                RememberMe = true
            }
        };

        var result = await model.OnPostAsync("/Accounts");

        result.Should().BeOfType<LocalRedirectResult>()
            .Which.Url.Should().Be("/Accounts");
    }

    [Fact]
    public async Task OnPostLoginAsync_WhenTwoFactorRequired_RedirectsTo2Fa()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(a => a.LoginUserAsync("test@Ledger.local", "Password123!", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.TwoFactorRequired);

        var model = new LoginModel(mockAuth.Object)
        {
            PageContext = CreatePageContext(),
            Input = new LoginModel.InputModel
            {
                Email = "test@Ledger.local",
                Password = "Password123!",
                RememberMe = false
            }
        };

        var result = await model.OnPostAsync("/Dashboard");

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Contain("/Security/LoginWith2fa");
    }

    [Fact]
    public async Task OnPostLoginAsync_WhenUserIsLockedOut_ReturnsPageWithError()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(a => a.LoginUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        var model = new LoginModel(mockAuth.Object)
        {
            PageContext = CreatePageContext(),
            Input = new LoginModel.InputModel { Email = "u@f.l", Password = "p" }
        };

        var result = await model.OnPostAsync("/Accounts");

        result.Should().BeOfType<PageResult>();
        model.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("verrouillé"));
    }

    [Fact]
    public async Task OnPostLoginAsync_WhenCredentialsAreInvalid_ReturnsPageWithGenericError()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(a => a.LoginUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var model = new LoginModel(mockAuth.Object)
        {
            PageContext = CreatePageContext(),
            Input = new LoginModel.InputModel { Email = "u@f.l", Password = "bad" }
        };

        var result = await model.OnPostAsync("/Accounts");

        result.Should().BeOfType<PageResult>();
        model.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("invalides"));
    }

    #endregion

    #region RegisterModel Tests

    [Fact]
    public void RegisterModel_OnGet_SetsReturnUrl()
    {
        var mockAuth = new Mock<IAuthService>();
        var model = new RegisterModel(mockAuth.Object);

        model.OnGet("/Accounts");

        model.ReturnUrl.Should().Be("/Accounts");
    }

    [Fact]
    public async Task OnPostRegisterAsync_WhenModelIsInvalid_ReturnsPage()
    {
        var mockAuth = new Mock<IAuthService>();
        var model = new RegisterModel(mockAuth.Object) { PageContext = CreatePageContext() };
        model.ModelState.AddModelError("Email", "Email requis");

        var result = await model.OnPostAsync("/Accounts");

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnPostRegisterAsync_WhenRegistrationSucceeds_ReturnsLocalRedirect()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(a => a.RegisterUserAsync("n@f.l", "Pass1234!", "New User"))
            .ReturnsAsync(IdentityResult.Success);
        mockAuth.Setup(a => a.LoginUserAsync("n@f.l", "Pass1234!", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var model = new RegisterModel(mockAuth.Object)
        {
            PageContext = CreatePageContext(),
            Input = new RegisterModel.InputModel
            {
                FullName = "New User",
                Email = "n@f.l",
                Password = "Pass1234!",
                ConfirmPassword = "Pass1234!"
            }
        };

        var result = await model.OnPostAsync("/Accounts");

        result.Should().BeOfType<LocalRedirectResult>();
    }

    [Fact]
    public async Task OnPostRegisterAsync_WhenRegistrationFails_AddsErrorsToModelState()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(a => a.RegisterUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email déjà utilisé." }));

        var model = new RegisterModel(mockAuth.Object)
        {
            PageContext = CreatePageContext(),
            Input = new RegisterModel.InputModel
            {
                FullName = "User",
                Email = "taken@f.l",
                Password = "P!",
                ConfirmPassword = "P!"
            }
        };

        var result = await model.OnPostAsync("/Accounts");

        result.Should().BeOfType<PageResult>();
        model.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage == "Email déjà utilisé.");
    }

    #endregion

    #region LogoutModel Tests

    [Fact]
    public async Task OnPostLogoutAsync_WhenInvoked_SignsOutAndRedirectsToLogin()
    {
        var mockAuth = new Mock<IAuthService>();
        var model = new LogoutModel(mockAuth.Object);

        var postResult = await model.OnPostAsync();
        postResult.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be("/Security/Login");

        var getResult = await model.OnGetAsync();
        getResult.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be("/Security/Login");

        mockAuth.Verify(a => a.SignOutAsync(), Times.Exactly(2));
    }

    #endregion

    #region LoginWith2faModel Tests

    private static (Mock<SignInManager<ApplicationUser>> mockSignIn, Mock<IAuthService> mockAuth) CreateAuthMocks()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var signInManager = new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            null!, null!, null!, null!);

        var authService = new Mock<IAuthService>();

        return (signInManager, authService);
    }

    [Fact]
    public async Task OnGet2faAsync_WhenNoUser_RedirectsToLogin()
    {
        var (mockSignIn, mockAuth) = CreateAuthMocks();
        mockSignIn.Setup(s => s.GetTwoFactorAuthenticationUserAsync())
            .ReturnsAsync((ApplicationUser?)null);

        var model = new LoginWith2faModel(mockSignIn.Object, mockAuth.Object);

        var result = await model.OnGetAsync(rememberMe: true, returnUrl: "/Accounts");

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be("/Security/Login");
    }

    [Fact]
    public async Task OnGet2faAsync_WhenValidUser_ReturnsPage()
    {
        var (mockSignIn, mockAuth) = CreateAuthMocks();
        mockSignIn.Setup(s => s.GetTwoFactorAuthenticationUserAsync())
            .ReturnsAsync(new ApplicationUser { UserName = "2fa-user" });

        var model = new LoginWith2faModel(mockSignIn.Object, mockAuth.Object);

        var result = await model.OnGetAsync(rememberMe: true, returnUrl: "/Accounts");

        result.Should().BeOfType<PageResult>();
        model.RememberMe.Should().BeTrue();
        model.ReturnUrl.Should().Be("/Accounts");
    }

    [Fact]
    public async Task OnPost2faAsync_WhenCodeIsValid_ReturnsLocalRedirect()
    {
        var (mockSignIn, mockAuth) = CreateAuthMocks();
        mockSignIn.Setup(s => s.GetTwoFactorAuthenticationUserAsync())
            .ReturnsAsync(new ApplicationUser { UserName = "2fa-user" });
        mockAuth.Setup(a => a.TwoFactorAuthenticatorSignInAsync("123456", false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var model = new LoginWith2faModel(mockSignIn.Object, mockAuth.Object)
        {
            PageContext = CreatePageContext(),
            Input = new LoginWith2faModel.InputModel
            {
                TwoFactorCode = "123456",
                RememberMachine = true
            }
        };

        var result = await model.OnPostAsync(rememberMe: false, returnUrl: "/Accounts");

        result.Should().BeOfType<LocalRedirectResult>();
    }

    [Fact]
    public async Task OnPost2faAsync_WhenUserLockedOut_ReturnsPageWithError()
    {
        var (mockSignIn, mockAuth) = CreateAuthMocks();
        mockSignIn.Setup(s => s.GetTwoFactorAuthenticationUserAsync())
            .ReturnsAsync(new ApplicationUser { UserName = "2fa-user" });
        mockAuth.Setup(a => a.TwoFactorAuthenticatorSignInAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        var model = new LoginWith2faModel(mockSignIn.Object, mockAuth.Object)
        {
            PageContext = CreatePageContext(),
            Input = new LoginWith2faModel.InputModel { TwoFactorCode = "000000" }
        };

        var result = await model.OnPostAsync(false, "/Accounts");

        result.Should().BeOfType<PageResult>();
        model.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("verrouillé"));
    }

    [Fact]
    public async Task OnPost2faAsync_WhenCodeIsInvalid_ReturnsPageWithError()
    {
        var (mockSignIn, mockAuth) = CreateAuthMocks();
        mockSignIn.Setup(s => s.GetTwoFactorAuthenticationUserAsync())
            .ReturnsAsync(new ApplicationUser { UserName = "2fa-user" });
        mockAuth.Setup(a => a.TwoFactorAuthenticatorSignInAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var model = new LoginWith2faModel(mockSignIn.Object, mockAuth.Object)
        {
            PageContext = CreatePageContext(),
            Input = new LoginWith2faModel.InputModel { TwoFactorCode = "999999" }
        };

        var result = await model.OnPostAsync(false, "/Accounts");

        result.Should().BeOfType<PageResult>();
        model.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("invalide"));
    }

    #endregion

    #region EnableMfaModel Tests

    [Fact]
    public async Task OnGetEnableMfaAsync_WhenUserNotFound_ReturnsNotFound()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var authService = new Mock<IAuthService>();
        var model = new EnableMfaModel(userManager.Object, authService.Object);

        var result = await model.OnGetAsync();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task OnGetEnableMfaAsync_WhenValidUser_PopulatesProperties()
    {
        var user = new ApplicationUser { Id = "test-uid", UserName = "mfa-user" };
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        userManager.Setup(m => m.GetTwoFactorEnabledAsync(user)).ReturnsAsync(false);

        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.GenerateMfaSetupAsync(user))
            .ReturnsAsync(new MfaSetupDto("KEY123", "otpauth://...", "data:image/png;base64,..."));

        var model = new EnableMfaModel(userManager.Object, authService.Object);

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        model.SharedKey.Should().Be("KEY123");
        model.Is2faEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task OnPostEnableMfaAsync_WhenCodeIsValid_EnablesMfaAndSetsRecoveryCodes()
    {
        var user = new ApplicationUser { Id = "test-uid", UserName = "mfa-user" };
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.VerifyAndEnableMfaAsync(user, "123456")).ReturnsAsync(true);
        authService.Setup(a => a.GenerateRecoveryCodesAsync(user)).ReturnsAsync(MockRecoveryCodes);

        var httpContext = new DefaultHttpContext();
        var tempDataProvider = new Mock<ITempDataProvider>();
        var tempData = new TempDataDictionary(httpContext, tempDataProvider.Object);

        var model = new EnableMfaModel(userManager.Object, authService.Object)
        {
            PageContext = CreatePageContext(),
            TempData = tempData,
            Input = new EnableMfaModel.InputModel { Code = "123456" }
        };

        var result = await model.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        model.Is2faEnabled.Should().BeTrue();
        model.RecoveryCodes.Should().Contain("REC-1");
        tempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task OnPostEnableMfaAsync_WhenCodeIsInvalid_AddsModelError()
    {
        var user = new ApplicationUser { Id = "test-uid", UserName = "mfa-user" };
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.VerifyAndEnableMfaAsync(user, "000000")).ReturnsAsync(false);
        authService.Setup(a => a.GenerateMfaSetupAsync(user))
            .ReturnsAsync(new MfaSetupDto("KEY", "URI", "QR"));

        var model = new EnableMfaModel(userManager.Object, authService.Object)
        {
            PageContext = CreatePageContext(),
            Input = new EnableMfaModel.InputModel { Code = "000000" }
        };

        var result = await model.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        model.ModelState["Input.Code"]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("incorrect"));
    }

    #endregion
}
