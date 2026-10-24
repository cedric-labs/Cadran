using System.Security.Claims;
using FluentAssertions;
using Ledger.Features.Security.Context;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Ledger.Tests.Security;

public class CurrentUserServiceTests
{
    [Fact]
    public void CurrentUserService_WhenUserAuthenticated_ShouldReturnUserIdAndEmail()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "usr-987654"),
            new Claim(ClaimTypes.Email, "alice@Ledger.local")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        var mockAccessor = new Mock<IHttpContextAccessor>();
        mockAccessor.Setup(a => a.HttpContext).Returns(context);

        var service = new CurrentUserService(mockAccessor.Object);

        service.IsAuthenticated.Should().BeTrue();
        service.UserId.Should().Be("usr-987654");
        service.UserEmail.Should().Be("alice@Ledger.local");
    }

    [Fact]
    public void CurrentUserService_WhenNoHttpContextOrAnonymous_ShouldReturnNullAndNotAuthenticated()
    {
        var mockAccessor = new Mock<IHttpContextAccessor>();
        mockAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var service = new CurrentUserService(mockAccessor.Object);

        service.IsAuthenticated.Should().BeFalse();
        service.UserId.Should().BeNull();
        service.UserEmail.Should().BeNull();
    }

    [Fact]
    public void CurrentUserService_WhenNoEmailClaim_FallsBackToIdentityName()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "usr-987654"),
            new Claim(ClaimTypes.Name, "alice_username")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        var mockAccessor = new Mock<IHttpContextAccessor>();
        mockAccessor.Setup(a => a.HttpContext).Returns(context);

        var service = new CurrentUserService(mockAccessor.Object);

        service.IsAuthenticated.Should().BeTrue();
        service.UserId.Should().Be("usr-987654");
        service.UserEmail.Should().Be("alice_username");
    }

    [Fact]
    public void CurrentUserService_WhenUserHasEmptyIdentityOrNullUser_ReturnsDefaults()
    {
        // 1. Contexte avec principal sans identité
        var contextWithoutIdentity = new DefaultHttpContext { User = new ClaimsPrincipal() };
        var mockAccessor1 = new Mock<IHttpContextAccessor>();
        mockAccessor1.Setup(a => a.HttpContext).Returns(contextWithoutIdentity);

        var service1 = new CurrentUserService(mockAccessor1.Object);
        service1.IsAuthenticated.Should().BeFalse();
        service1.UserId.Should().BeNull();
        service1.UserEmail.Should().BeNull();

        // 2. Contexte avec identité non authentifiée
        var unauthenticatedIdentity = new ClaimsIdentity(); // pas d'authenticationType => IsAuthenticated = false
        var contextUnauth = new DefaultHttpContext { User = new ClaimsPrincipal(unauthenticatedIdentity) };
        var mockAccessor2 = new Mock<IHttpContextAccessor>();
        mockAccessor2.Setup(a => a.HttpContext).Returns(contextUnauth);

        var service2 = new CurrentUserService(mockAccessor2.Object);
        service2.IsAuthenticated.Should().BeFalse();
        service2.UserId.Should().BeNull();
        service2.UserEmail.Should().BeNull();
    }
}
