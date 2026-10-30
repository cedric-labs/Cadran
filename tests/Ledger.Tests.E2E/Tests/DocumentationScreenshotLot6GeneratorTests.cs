using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Services;
using Ledger.Features.Reports.Services;
using Ledger.Features.Security.Context;
using Ledger.Features.Security.Models;
using Ledger.Infrastructure.Data;
using Ledger.Tests.E2E.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Generates real, annotated screenshots for Batch 6 (Financial Reports & Appearance Settings).
/// Strict end-user actionable approach: zero technical jargon, clean ring + numbered pins, 100% in English.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class DocumentationScreenshotLot6GeneratorTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    private static readonly string ArtifactsDirectory = Path.Combine(Path.GetTempPath(), "cadran-doc-screenshots");

    [Fact]
    public async Task GenerateLot6Screenshots_WhenExecuted_CapturesAllScreensInEnglish()
    {
        Directory.CreateDirectory(ArtifactsDirectory);

        var keyHolder = WebFixture.KestrelServices.GetRequiredService<IDatabaseKeyHolder>();
        keyHolder.SetKey(null, isEncrypted: false);

        // 1. Initialiser l'utilisateur et les dashboards par défaut
        await SeedUserDataAsync();

        // 2. Se connecter dans Playwright avec cet utilisateur et activer la langue anglaise
        await LoginAndSetEnglishAsync();

        // 3. Écran 22 - Financial Reports & Analytics (/Reports)
        await CaptureReportsScreenAsync();

        // 4. Écran 23 - Appearance & Font Preferences (/Settings/Appearance)
        await CaptureAppearanceSettingsScreenAsync();
    }

    private async Task SeedUserDataAsync()
    {
        using var scope = WebFixture.KestrelServices.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var accountTypeService = scope.ServiceProvider.GetRequiredService<IAccountTypeService>();
        var categorySeedService = scope.ServiceProvider.GetRequiredService<ICategorySeedService>();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();

        const string email = "sarah.connor@cadran.local";
        const string password = "Password123";

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "Sarah Connor",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, password);
            result.Succeeded.Should().BeTrue();
        }

        var userId = user.Id;
        await accountTypeService.EnsureDefaultAccountTypesForUserAsync(userId);
        await categorySeedService.EnsureDefaultCategoriesForUserAsync(userId);

        var boursoBank = await db.Banks.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.UserId == userId && b.Name == "BoursoBank");
        if (boursoBank == null)
        {
            boursoBank = new Bank { UserId = userId, Name = "BoursoBank", ColorHex = "#0284c7" };
            db.Banks.Add(boursoBank);
            await db.SaveChangesAsync();
        }

        var types = await db.CustomAccountTypes.IgnoreQueryFilters().Where(t => t.UserId == userId).ToListAsync();
        var checkingType = types.FirstOrDefault(t => string.Equals(t.Code, "Checking", StringComparison.Ordinal));

        var checkingAcc = await db.BankAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.UserId == userId && a.Name == "Main Checking Account");
        if (checkingAcc == null)
        {
            checkingAcc = new BankAccount
            {
                UserId = userId,
                BankId = boursoBank.Id,
                Name = "Main Checking Account",
                CustomAccountTypeId = checkingType?.Id,
                Type = AccountType.Checking,
                Currency = "EUR",
                InitialBalance = 2845.50m
            };
            db.BankAccounts.Add(checkingAcc);
            await db.SaveChangesAsync();
        }

        // Réinitialiser les dashboards et widgets préconfigurés pour l'utilisateur
        await reportService.ResetDefaultDashboardAsync(CancellationToken.None);
    }

    private async Task LoginAndSetEnglishAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/Security/Login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.FillAsync("input[name='Input.Email']", "sarah.connor@cadran.local");
        await Page.FillAsync("input[name='Input.Password']", "Password123");
        await Page.ClickAsync("button[type='submit']");
        await Page.WaitForURLAsync("**/Accounts*");

        // Activer la langue anglaise
        await Page.GotoAsync($"{BaseUrl}/SetLanguage?culture=en&returnUrl=%2FReports", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    private async Task CaptureReportsScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 950);
        await Page.GotoAsync($"{BaseUrl}/Reports", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var navPills = Page.Locator("ul.nav-pills");
        (await navPills.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ArtifactsDirectory, "22-financial-reports-screen.png");
        var annotatedPath = Path.Combine(ArtifactsDirectory, "22-financial-reports-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""ul.nav-pills"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""button[data-bs-toggle='dropdown'].dropdown-toggle"", number: '2', actionColor: '#0284c7' },
                { targetSelector: "".row.g-3 > div:nth-child(1) .card"", number: '3', actionColor: '#16a34a' },
                { targetSelector: "".row.g-3 > div:nth-child(2) .card"", number: '4', actionColor: '#ca8a04' },
                { targetSelector: ""button[data-bs-target='#addWidgetModal']"", number: '5', actionColor: '#9333ea' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-reports';
            container.style.position = 'absolute';
            container.style.top = '0';
            container.style.left = '0';
            container.style.width = '100%';
            container.style.height = '100%';
            container.style.pointerEvents = 'none';
            container.style.zIndex = '999999';
            document.body.appendChild(container);

            items.forEach((item) => {
                const target = document.querySelector(item.targetSelector);
                if (!target) return;

                const tRect = target.getBoundingClientRect();

                const ring = document.createElement('div');
                ring.style.position = 'absolute';
                ring.style.left = `${tRect.left - 3}px`;
                ring.style.top = `${tRect.top - 3}px`;
                ring.style.width = `${tRect.width + 6}px`;
                ring.style.height = `${tRect.height + 6}px`;
                ring.style.border = `2px solid ${item.actionColor}`;
                ring.style.borderRadius = '6px';
                ring.style.boxShadow = `0 0 0 3px ${item.actionColor}25`;
                container.appendChild(ring);

                const pin = document.createElement('div');
                pin.textContent = item.number;
                pin.style.position = 'absolute';
                pin.style.left = `${tRect.left - 10}px`;
                pin.style.top = `${tRect.top - 10}px`;
                pin.style.width = '24px';
                pin.style.height = '24px';
                pin.style.borderRadius = '50%';
                pin.style.backgroundColor = item.actionColor;
                pin.style.color = '#fff';
                pin.style.fontSize = '12px';
                pin.style.fontWeight = '700';
                pin.style.display = 'flex';
                pin.style.alignItems = 'center';
                pin.style.justifyContent = 'center';
                pin.style.border = '2px solid #fff';
                pin.style.boxShadow = '0 2px 6px rgba(0,0,0,0.35)';
                pin.style.zIndex = '100002';
                container.appendChild(pin);
            });
        }");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = annotatedPath, FullPage = false });
    }

    private async Task CaptureAppearanceSettingsScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 950);
        await Page.GotoAsync($"{BaseUrl}/Settings/Appearance", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var fontCard = Page.Locator("#fontOptionsContainer .font-card").First;
        (await fontCard.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ArtifactsDirectory, "23-appearance-settings-screen.png");
        var annotatedPath = Path.Combine(ArtifactsDirectory, "23-appearance-settings-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""button[data-bs-target='#addFontModal']"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""#fontOptionsContainer .font-card:first-child"", number: '2', actionColor: '#0284c7' },
                { targetSelector: "".size-card:first-child"", number: '3', actionColor: '#16a34a' },
                { targetSelector: "".mode-card:first-child"", number: '4', actionColor: '#ca8a04' },
                { targetSelector: ""button[type='submit'].btn-primary"", number: '5', actionColor: '#16a34a' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-appearance';
            container.style.position = 'absolute';
            container.style.top = '0';
            container.style.left = '0';
            container.style.width = '100%';
            container.style.height = '100%';
            container.style.pointerEvents = 'none';
            container.style.zIndex = '999999';
            document.body.appendChild(container);

            items.forEach((item) => {
                const target = document.querySelector(item.targetSelector);
                if (!target) return;

                const tRect = target.getBoundingClientRect();

                const ring = document.createElement('div');
                ring.style.position = 'absolute';
                ring.style.left = `${tRect.left - 3}px`;
                ring.style.top = `${tRect.top - 3}px`;
                ring.style.width = `${tRect.width + 6}px`;
                ring.style.height = `${tRect.height + 6}px`;
                ring.style.border = `2px solid ${item.actionColor}`;
                ring.style.borderRadius = '6px';
                ring.style.boxShadow = `0 0 0 3px ${item.actionColor}25`;
                container.appendChild(ring);

                const pin = document.createElement('div');
                pin.textContent = item.number;
                pin.style.position = 'absolute';
                pin.style.left = `${tRect.left - 10}px`;
                pin.style.top = `${tRect.top - 10}px`;
                pin.style.width = '24px';
                pin.style.height = '24px';
                pin.style.borderRadius = '50%';
                pin.style.backgroundColor = item.actionColor;
                pin.style.color = '#fff';
                pin.style.fontSize = '12px';
                pin.style.fontWeight = '700';
                pin.style.display = 'flex';
                pin.style.alignItems = 'center';
                pin.style.justifyContent = 'center';
                pin.style.border = '2px solid #fff';
                pin.style.boxShadow = '0 2px 6px rgba(0,0,0,0.35)';
                pin.style.zIndex = '100002';
                container.appendChild(pin);
            });
        }");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = annotatedPath, FullPage = false });
    }
}
