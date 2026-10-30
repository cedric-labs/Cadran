using System.Globalization;
using System.Net;
using AwesomeAssertions;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Imports.Services;
using Ledger.Features.Imports.Strategies;
using Ledger.Features.Merchants.Services;
using Ledger.Features.Recurring.Services;
using Ledger.Features.Reports.Services;
using Ledger.Features.RulesEngine.Services;
using Ledger.Features.Security.Context;
using Ledger.Features.Security.Models;
using Ledger.Features.Security.Services;
using Ledger.Features.Settings.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace Ledger.Tests.Pages;

public class ProgramTests : IClassFixture<AppWebApplicationFactory>
{
    private readonly AppWebApplicationFactory _factory;

    public ProgramTests(AppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Program_ServiceResolution_RegistersAllRequiredServices()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        // Infrastructure & DB
        sp.GetRequiredService<AppDbContext>().Should().NotBeNull();
        sp.GetRequiredService<IDatabaseKeyHolder>().Should().NotBeNull();
        sp.GetRequiredService<IDatabaseKeyStore>().Should().NotBeNull();
        sp.GetRequiredService<IDatabaseConnectionProvider>().Should().NotBeNull();
        sp.GetRequiredService<IDatabaseEncryptionService>().Should().NotBeNull();

        // Identity & Security
        sp.GetRequiredService<UserManager<ApplicationUser>>().Should().NotBeNull();
        sp.GetRequiredService<ICurrentUserService>().Should().NotBeNull();
        sp.GetRequiredService<IAuthService>().Should().NotBeNull();

        // Slices Métier
        sp.GetRequiredService<IAccountBalanceService>().Should().NotBeNull();
        sp.GetRequiredService<IAccountTypeService>().Should().NotBeNull();
        sp.GetRequiredService<ITransactionStatusService>().Should().NotBeNull();
        sp.GetRequiredService<Ledger.Features.Categories.Services.ICategorySeedService>().Should().NotBeNull();
        sp.GetRequiredService<IMerchantService>().Should().NotBeNull();
        sp.GetRequiredService<IRulesEngineService>().Should().NotBeNull();
        sp.GetRequiredService<ITransactionImportService>().Should().NotBeNull();
        sp.GetServices<IBankStatementParser>().Should().HaveCountGreaterThanOrEqualTo(2);
        sp.GetRequiredService<IRecurringExpenseService>().Should().NotBeNull();
        sp.GetRequiredService<IReportService>().Should().NotBeNull();
        sp.GetRequiredService<IPivotReportService>().Should().NotBeNull();
        sp.GetRequiredService<IUiSettingsService>().Should().NotBeNull();

        // Localisation & Configuration
        sp.GetRequiredService<ILanguageService>().Should().NotBeNull();
        sp.GetRequiredService<ILocalizedEntityService>().Should().NotBeNull();
        sp.GetRequiredService<ICurrencyService>().Should().NotBeNull();
        sp.GetRequiredService<IStringLocalizerFactory>().Should().NotBeNull();
        sp.GetRequiredService<IStringLocalizer>().Should().NotBeNull();
    }

    [Theory]
    [InlineData("/Security/Login")]
    [InlineData("/Security/Register")]
    [InlineData("/")]
    public async Task Program_PublicPages_AllowAnonymousAccess(string url)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginWith2faEndpoint_WithoutContext_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Security/LoginWith2fa");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/Security/Login");
    }

    [Fact]
    public async Task UnlockDatabaseEndpoint_WhenAlreadyUnlocked_RedirectsToHome()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Security/UnlockDatabase");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Theory]
    [InlineData("/Accounts")]
    [InlineData("/Categories")]
    [InlineData("/Merchants")]
    [InlineData("/RulesEngine")]
    [InlineData("/Imports")]
    [InlineData("/Recurring")]
    [InlineData("/Reports")]
    [InlineData("/Settings/Appearance")]
    [InlineData("/Security/EnableMfa")]
    [InlineData("/Security/DatabaseEncryption")]
    public async Task Program_ProtectedPages_RedirectUnauthenticatedUsersToLogin(string url)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/Security/Login");
    }

    [Fact]
    public async Task SetLanguageEndpoint_WhenCultureValid_SetsCookieAndRedirects()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/SetLanguage?culture=en&returnUrl=/Index");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Be("/Index");

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(c => c.Contains(".AspNetCore.Culture=") && c.Contains("c%3Den"));
    }

    [Fact]
    public async Task SetLanguageEndpoint_WhenCultureUnsupported_FallsBackToFrenchAndRoot()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/SetLanguage?culture=unknown_lang");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Be("/");

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(c => c.Contains(".AspNetCore.Culture=") && c.Contains("c%3Dfr"));
    }

    [Fact]
    public async Task DatabaseLockMiddleware_WhenLocked_RedirectsToUnlockDatabase()
    {
        var keyHolder = _factory.Services.GetRequiredService<IDatabaseKeyHolder>();
        try
        {
            keyHolder.SetKey(null, isEncrypted: true);

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.GetAsync("/Index");

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().Should().Contain("/Security/UnlockDatabase");

            var unlockGetResponse = await client.GetAsync("/Security/UnlockDatabase");
            unlockGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            keyHolder.SetKey(null, isEncrypted: false);
        }
    }

    [Fact]
    public void ConfigureLocalization_WhenDefaultThreadCulture_UsesEuroSymbol()
    {
        CultureInfo.DefaultThreadCurrentCulture.Should().NotBeNull();
        CultureInfo.DefaultThreadCurrentCulture.NumberFormat.CurrencySymbol.Should().Be("€");
        CultureInfo.DefaultThreadCurrentUICulture!.NumberFormat.CurrencySymbol.Should().Be("€");
    }

    [Fact]
    public void Program_ServerPortAndNetworkConfiguration_ConfiguresHostProperly()
    {
        using var customFactory = new AppWebApplicationFactory();
        using var client = customFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Server:Port"] = "5055",
                    ["Server:AllowNetworkAccess"] = "true",
                    ["Server:AutoLaunchBrowser"] = "false"
                });
            });
        }).CreateClient();

        client.Should().NotBeNull();
    }
}
