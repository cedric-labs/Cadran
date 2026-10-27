using AwesomeAssertions;
using Ledger.Features.Security.Context;
using Ledger.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Ledger.Tests.Security;

public class DatabaseLockMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenDatabaseUnlocked_InvokesNextDelegate()
    {
        var keyHolder = new DatabaseKeyHolder();
        keyHolder.SetKey("Secret", isEncrypted: true);

        bool nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new DatabaseLockMiddleware(next);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/Accounts";

        await middleware.InvokeAsync(httpContext, keyHolder);

        nextCalled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().NotBe(302);
    }

    [Fact]
    public async Task InvokeAsync_WhenDatabaseLocked_RedirectsToUnlockDatabase()
    {
        var keyHolder = new DatabaseKeyHolder();
        keyHolder.SetKey(null, isEncrypted: true);

        bool nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new DatabaseLockMiddleware(next);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/Accounts";
        httpContext.Request.QueryString = new QueryString("?sort=desc");

        await middleware.InvokeAsync(httpContext, keyHolder);

        nextCalled.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(302);
        httpContext.Response.Headers.Location.ToString().Should().StartWith("/Security/UnlockDatabase");
        httpContext.Response.Headers.Location.ToString().Should().Contain("%2FAccounts");
    }

    [Fact]
    public async Task InvokeAsync_WhenDatabaseLockedAndAccessingStaticAssets_AllowsAccess()
    {
        var keyHolder = new DatabaseKeyHolder();
        keyHolder.SetKey(null, isEncrypted: true);

        bool nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new DatabaseLockMiddleware(next);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/lib/bootstrap/dist/css/bootstrap.min.css";

        await middleware.InvokeAsync(httpContext, keyHolder);

        nextCalled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().NotBe(302);
    }

    [Fact]
    public async Task InvokeAsync_WhenDatabaseLockedAndAccessingUnlockPage_AllowsAccess()
    {
        var keyHolder = new DatabaseKeyHolder();
        keyHolder.SetKey(null, isEncrypted: true);

        bool nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new DatabaseLockMiddleware(next);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/Security/UnlockDatabase";

        await middleware.InvokeAsync(httpContext, keyHolder);

        nextCalled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().NotBe(302);
    }

    [Fact]
    public async Task InvokeAsync_WhenDatabaseUnlockedAndAccessingUnlockPage_RedirectsToHome()
    {
        var keyHolder = new DatabaseKeyHolder(); // Not encrypted => unlocked

        bool nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new DatabaseLockMiddleware(next);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/Security/UnlockDatabase";

        await middleware.InvokeAsync(httpContext, keyHolder);

        nextCalled.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(302);
        httpContext.Response.Headers.Location.ToString().Should().Be("/");
    }
}
