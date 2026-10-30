using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Services;
using Ledger.Features.Imports.Entities;
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
/// Generates real, annotated screenshots for Batch 5 (Bank Imports & CSV Mapping Profiles).
/// Strict end-user actionable approach: zero technical jargon, clean ring + numbered pins, 100% in English.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class DocumentationScreenshotLot5GeneratorTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    private static readonly string ArtifactsDirectory = Path.Combine(Path.GetTempPath(), "cadran-doc-screenshots");

    [Fact]
    public async Task GenerateLot5Screenshots_WhenExecuted_CapturesAllScreensInEnglish()
    {
        Directory.CreateDirectory(ArtifactsDirectory);

        var keyHolder = WebFixture.KestrelServices.GetRequiredService<IDatabaseKeyHolder>();
        keyHolder.SetKey(null, isEncrypted: false);

        // 1. Initialiser l'utilisateur, compte, profil CSV et historique d'import
        await SeedUserDataAsync();

        // 2. Se connecter dans Playwright avec cet utilisateur et activer la langue anglaise
        await LoginAndSetEnglishAsync();

        // 3. Écran 20 - Statement Import (/Imports)
        await CaptureStatementImportsScreenAsync();

        // 4. Écran 21 - CSV Mapping Profile Builder (/Imports/CsvProfiles)
        await CaptureCsvProfilesScreenAsync();
    }

    private async Task SeedUserDataAsync()
    {
        using var scope = WebFixture.KestrelServices.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var accountTypeService = scope.ServiceProvider.GetRequiredService<IAccountTypeService>();
        var categorySeedService = scope.ServiceProvider.GetRequiredService<ICategorySeedService>();

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
                InitialBalance = 2845.50m,
                OfxAccountId = "BOURSO-CHK-01"
            };
            db.BankAccounts.Add(checkingAcc);
            await db.SaveChangesAsync();
        }

        // Profil CSV de démonstration
        var existingProfile = await db.CsvMappingProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.UserId == userId && p.Name.Contains("BoursoBank"));
        if (existingProfile == null)
        {
            db.CsvMappingProfiles.Add(new CsvMappingProfile
            {
                UserId = userId,
                Name = "BoursoBank CSV Profile",
                Delimiter = ";",
                DecimalSeparator = ",",
                DateFormat = "dd/MM/yyyy",
                HasHeader = true,
                DateColumnIndex = 0,
                LabelColumnIndex = 2,
                AmountColumnIndex = 3
            });
            await db.SaveChangesAsync();
        }

        // Lot d'import d'historique
        var existingBatches = await db.ImportBatches.IgnoreQueryFilters().Where(b => b.UserId == userId).ToListAsync();
        if (existingBatches.Count == 0)
        {
            db.ImportBatches.Add(new ImportBatch
            {
                UserId = userId,
                BankAccountId = checkingAcc.Id,
                FileName = "bourso_releve_mensuel_octobre_2026.csv",
                Format = "CSV",
                TotalParsed = 24,
                TotalImported = 24,
                TotalDuplicatesSkipped = 0,
                TotalAutoCategorized = 19
            });
            await db.SaveChangesAsync();
        }
    }

    private async Task LoginAndSetEnglishAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/Security/Login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.FillAsync("input[name='Input.Email']", "sarah.connor@cadran.local");
        await Page.FillAsync("input[name='Input.Password']", "Password123");
        await Page.ClickAsync("button[type='submit']");
        await Page.WaitForURLAsync("**/Accounts*");

        // Activer la langue anglaise
        await Page.GotoAsync($"{BaseUrl}/SetLanguage?culture=en&returnUrl=%2FImports", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    private async Task CaptureStatementImportsScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 950);
        await Page.GotoAsync($"{BaseUrl}/Imports", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var header = Page.Locator("a.btn[href='/Imports/CsvProfiles']");
        (await header.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ArtifactsDirectory, "20-statement-imports-screen.png");
        var annotatedPath = Path.Combine(ArtifactsDirectory, "20-statement-imports-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""a.btn[href='/Imports/CsvProfiles']"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""#inputBankAccountId"", number: '2', actionColor: '#0284c7' },
                { targetSelector: ""label[for='inputFileUpload']"", number: '3', actionColor: '#16a34a' },
                { targetSelector: ""#inputCsvProfileId"", number: '4', actionColor: '#ca8a04' },
                { targetSelector: ""form button[type='submit']"", number: '5', actionColor: '#16a34a' },
                { targetSelector: "".card table tbody tr:first-child"", number: '6', actionColor: '#0d9488' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-imports';
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

    private async Task CaptureCsvProfilesScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 950);
        await Page.GotoAsync($"{BaseUrl}/Imports/CsvProfiles", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Ouvrir le formulaire de création de profil
        var btnNewProfile = Page.Locator("button.btn-primary[data-bs-target='#newProfileCollapse']");
        await btnNewProfile.ClickAsync();
        await Page.WaitForSelectorAsync("#newProfileCollapse.show");
        await Page.WaitForTimeoutAsync(400);

        var cleanPath = Path.Combine(ArtifactsDirectory, "21-import-mapping-screen.png");
        var annotatedPath = Path.Combine(ArtifactsDirectory, "21-import-mapping-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""button.btn-primary[data-bs-target='#newProfileCollapse']"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""#inputProfileName"", number: '2', actionColor: '#0284c7' },
                { targetSelector: ""#inputProfileDelimiter"", number: '3', actionColor: '#ca8a04' },
                { targetSelector: ""#inputProfileDateFormat"", number: '4', actionColor: '#16a34a' },
                { targetSelector: ""#inputProfileHasHeader"", number: '5', actionColor: '#9333ea' },
                { targetSelector: ""#inputProfileDateCol"", number: '6', actionColor: '#0d9488' },
                { targetSelector: ""#newProfileCollapse button[type='submit']"", number: '7', actionColor: '#16a34a' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-csv-profiles';
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
