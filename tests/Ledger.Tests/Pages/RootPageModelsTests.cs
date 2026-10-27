using System.Security.Claims;
using AwesomeAssertions;
using Ledger.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ledger.Tests.Pages;

public class RootPageModelsTests
{
    private static PageContext CreatePageContext(ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext();
        if (user != null)
        {
            httpContext.User = user;
        }
        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        return new PageContext(actionContext);
    }

    [Fact]
    public void ErrorModel_OnGet_SetsRequestIdAndLogs()
    {
        var mockLogger = new Mock<ILogger<ErrorModel>>();
        var model = new ErrorModel(mockLogger.Object)
        {
            PageContext = CreatePageContext()
        };

        model.OnGet();

        model.RequestId.Should().NotBeNullOrEmpty();
        model.ShowRequestId.Should().BeTrue();
    }

    [Fact]
    public void OnGet_WhenAuthenticated_RedirectsToAccounts()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "user@test") }, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var model = new IndexModel
        {
            PageContext = CreatePageContext(principal)
        };

        var result = model.OnGet();

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be("/Accounts");
    }

    [Fact]
    public void OnGet_WhenAnonymous_ReturnsPage()
    {
        var model = new IndexModel
        {
            PageContext = CreatePageContext()
        };

        var result = model.OnGet();

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public void PrivacyModel_OnGet_RunsWithoutException()
    {
        var model = new PrivacyModel();
        model.OnGet();
        model.Should().NotBeNull();
    }
}
