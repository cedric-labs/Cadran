using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Categories.Services;
using Ledger.Features.Merchants.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.RulesEngine.Entities;
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
/// Generates real, annotated screenshots for Batch 4 (Categories, Rules Engine & Recurring Operations).
/// Strict end-user actionable approach: zero technical jargon, clean ring + numbered pins, 100% in English.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class DocumentationScreenshotLot4GeneratorTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    private static readonly string ArtifactsDirectory = Path.Combine(Path.GetTempPath(), "cadran-doc-screenshots");

    private static string ProjectDocsDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/images/screens"));

    [Fact]
    public async Task GenerateLot4Screenshots_WhenExecuted_CapturesAllScreensInEnglish()
    {
        Directory.CreateDirectory(ProjectDocsDir);
        Directory.CreateDirectory(ArtifactsDirectory);

        var keyHolder = WebFixture.KestrelServices.GetRequiredService<IDatabaseKeyHolder>();
        keyHolder.SetKey(null, isEncrypted: false);

        // 1. Initialiser l'utilisateur, catégories, règles et opérations récurrentes
        await SeedUserDataAsync();

        // 2. Se connecter dans Playwright avec cet utilisateur et activer la langue anglaise
        await LoginAndSetEnglishAsync();

        // 3. Écran 16 - Category Hierarchy & Tree Explorer (/Categories)
        await CaptureCategoriesScreenAsync();

        // 4. Écran 17 - Create / Edit Category Modal (/Categories/CreateOrEdit)
        await CaptureCreateCategoryScreenAsync();

        // 5. Écran 18 - Automated Rules Engine (/RulesEngine)
        await CaptureRulesEngineScreenAsync();

        // 6. Écran 19 - Recurring Operations & Bills Schedule (/Recurring)
        await CaptureRecurringExpensesScreenAsync();
    }

    private async Task SeedUserDataAsync()
    {
        using var scope = WebFixture.KestrelServices.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var accountTypeService = scope.ServiceProvider.GetRequiredService<IAccountTypeService>();
        var statusService = scope.ServiceProvider.GetRequiredService<ITransactionStatusService>();
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
        await statusService.EnsureDefaultStatusesForUserAsync(userId);
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

        var categories = await db.Categories.IgnoreQueryFilters().Where(c => c.UserId == userId).ToListAsync();
        var catHousing = categories.FirstOrDefault(c => c.Name.Contains("Logement", StringComparison.Ordinal) || c.Name.Contains("Housing", StringComparison.Ordinal));
        var catUtilities = categories.FirstOrDefault(c => c.Name.Contains("Énergie", StringComparison.Ordinal) || c.Name.Contains("Utilities", StringComparison.Ordinal) || c.Name.Contains("Factures", StringComparison.Ordinal));
        var catFood = categories.FirstOrDefault(c => c.Name.Contains("Alimentation", StringComparison.Ordinal) || c.Name.Contains("Food", StringComparison.Ordinal) || c.Name.Contains("Courses", StringComparison.Ordinal));
        var catInternet = categories.FirstOrDefault(c => c.Name.Contains("Abonnements", StringComparison.Ordinal) || c.Name.Contains("Subscriptions", StringComparison.Ordinal));

        // Marchand Carrefour
        var carrefour = await db.Merchants.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.UserId == userId && m.Name == "Carrefour Market");
        if (carrefour == null)
        {
            carrefour = new Merchant { UserId = userId, Name = "Carrefour Market", NormalizedName = "CARREFOUR MARKET" };
            db.Merchants.Add(carrefour);
            await db.SaveChangesAsync();
        }

        // Règles de catégorisation
        var existingRules = await db.CategorizationRules.IgnoreQueryFilters().Where(r => r.UserId == userId).ToListAsync();
        if (existingRules.Count == 0)
        {
            db.CategorizationRules.AddRange(
                new CategorizationRule
                {
                    UserId = userId,
                    Name = "Supermarkets & Groceries",
                    Pattern = "(?i)(carrefour|auchan|lidl|monoprix|leclerc)",
                    Priority = 10,
                    TargetCategoryId = catFood?.Id ?? categories[0].Id,
                    TargetMerchantId = carrefour.Id
                },
                new CategorizationRule
                {
                    UserId = userId,
                    Name = "Electricity & Energy Bills",
                    Pattern = "(?i)(edf|engie|totalenergies)",
                    Priority = 20,
                    TargetCategoryId = catUtilities?.Id ?? categories[0].Id
                },
                new CategorizationRule
                {
                    UserId = userId,
                    Name = "Apartment Monthly Lease",
                    Pattern = "(?i)(loyer|immobilier|bail)",
                    Priority = 30,
                    TargetCategoryId = catHousing?.Id ?? categories[0].Id
                }
            );
            await db.SaveChangesAsync();
        }

        // Charges récurrentes
        var existingRecurring = await db.RecurringExpenses.IgnoreQueryFilters().Where(r => r.UserId == userId).ToListAsync();
        if (existingRecurring.Count == 0)
        {
            db.RecurringExpenses.AddRange(
                new RecurringExpense
                {
                    UserId = userId,
                    Name = "Apartment Monthly Rent",
                    Pattern = "IMMOBILIER PARIS",
                    ExpectedAmount = 950.00m,
                    Frequency = RecurringFrequency.Monthly,
                    ExpectedDayOfMonth = 5,
                    TargetCategoryId = catHousing?.Id,
                    TargetBankAccountId = checkingAcc.Id
                },
                new RecurringExpense
                {
                    UserId = userId,
                    Name = "Fiber High-Speed Internet",
                    Pattern = "FIBER HIGH-SPEED",
                    ExpectedAmount = 39.99m,
                    Frequency = RecurringFrequency.Monthly,
                    ExpectedDayOfMonth = 10,
                    TargetCategoryId = catInternet?.Id,
                    TargetBankAccountId = checkingAcc.Id
                },
                new RecurringExpense
                {
                    UserId = userId,
                    Name = "Electricity & Heating Bill",
                    Pattern = "EDF ENERGY",
                    ExpectedAmount = 86.50m,
                    Frequency = RecurringFrequency.Monthly,
                    ExpectedDayOfMonth = 15,
                    TargetCategoryId = catUtilities?.Id,
                    TargetBankAccountId = checkingAcc.Id
                }
            );
            await db.SaveChangesAsync();
        }

        // Transactions du mois courant pour alimenter la synthèse récurrente
        var now = DateTime.Today;
        var existingTx = await db.Transactions.IgnoreQueryFilters().Where(t => t.UserId == userId).ToListAsync();
        if (existingTx.Count == 0)
        {
            db.Transactions.AddRange(
                new Transaction
                {
                    UserId = userId,
                    BankAccountId = checkingAcc.Id,
                    Date = new DateTime(now.Year, now.Month, 5, 0, 0, 0, DateTimeKind.Utc),
                    Label = "Immobilier Paris - Apartment Monthly Rent",
                    Amount = 950.00m,
                    Type = TransactionType.Debit,
                    Status = TransactionStatus.Reconciled,
                    CategoryId = catHousing?.Id
                },
                new Transaction
                {
                    UserId = userId,
                    BankAccountId = checkingAcc.Id,
                    Date = new DateTime(now.Year, now.Month, 10, 0, 0, 0, DateTimeKind.Utc),
                    Label = "Fiber High-Speed Internet & Mobile",
                    Amount = 39.99m,
                    Type = TransactionType.Debit,
                    Status = TransactionStatus.Cleared,
                    CategoryId = catInternet?.Id
                }
            );
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
        await Page.GotoAsync($"{BaseUrl}/SetLanguage?culture=en&returnUrl=%2FCategories", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    private async Task CaptureCategoriesScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 950);
        await Page.GotoAsync($"{BaseUrl}/Categories", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var newCatBtn = Page.Locator("a[href='/Categories/CreateOrEdit']");
        (await newCatBtn.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "16-categories-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "16-categories-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "16-categories-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""a[href='/Categories/CreateOrEdit']"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""#btnToggleAllCategories"", number: '2', actionColor: '#475569' },
                { targetSelector: "".category-group-card:first-child .card-header .rounded-circle"", number: '3', actionColor: '#16a34a' },
                { targetSelector: "".category-group-card:first-child .card-header button.toggle-collapse-btn"", number: '4', actionColor: '#0284c7' },
                { targetSelector: "".category-group-card:first-child .card-header .dropdown button"", number: '5', actionColor: '#ca8a04' },
                { targetSelector: "".category-group-card:first-child .card-body a[href^='/Categories/CreateOrEdit/']"", number: '6', actionColor: '#0d9488' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-categories';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "16-categories-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureCreateCategoryScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 950);
        await Page.GotoAsync($"{BaseUrl}/Categories/CreateOrEdit", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var nameInput = Page.Locator("#mainCategoryNameInput");
        (await nameInput.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "17-create-category-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "17-create-category-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "17-create-category-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""#mainCategoryNameInput"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""#languageSelect"", number: '2', actionColor: '#0284c7' },
                { targetSelector: ""#inputColorPicker"", number: '3', actionColor: '#ca8a04' },
                { targetSelector: ""#inputParentCategoryId"", number: '4', actionColor: '#16a34a' },
                { targetSelector: ""#icon-picker-component button"", number: '5', actionColor: '#9333ea' },
                { targetSelector: ""#categoryForm button[type='submit']"", number: '6', actionColor: '#16a34a' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-create-category';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "17-create-category-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureRulesEngineScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 950);
        await Page.GotoAsync($"{BaseUrl}/RulesEngine", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Ouvrir le formulaire de nouvelle règle
        var btnAdd = Page.Locator("#btnToggleNewRule");
        await btnAdd.ClickAsync();
        await Page.WaitForSelectorAsync("#newRuleCollapse.show");
        await Page.WaitForTimeoutAsync(400);

        var cleanPath = Path.Combine(ProjectDocsDir, "18-rules-engine-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "18-rules-engine-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "18-rules-engine-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""#btnToggleNewRule"", number: '1', actionColor: '#2563eb' },
                { targetSelector: "".btn-open-apply-modal"", number: '2', actionColor: '#16a34a' },
                { targetSelector: ""#createRuleName"", number: '3', actionColor: '#0284c7' },
                { targetSelector: ""#createRuleCategory"", number: '4', actionColor: '#ca8a04' },
                { targetSelector: ""#createKeywordInput"", number: '5', actionColor: '#9333ea' },
                { targetSelector: ""#createTestInput"", number: '6', actionColor: '#0d9488' },
                { targetSelector: ""#createRuleForm button[type='submit']"", number: '7', actionColor: '#16a34a' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-rules-engine';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "18-rules-engine-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureRecurringExpensesScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 950);
        await Page.GotoAsync($"{BaseUrl}/Recurring", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var newBtn = Page.Locator("button[data-bs-target='#editModal']");
        (await newBtn.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "19-recurring-transactions-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "19-recurring-transactions-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "19-recurring-transactions-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: "".btn-group.shadow-sm"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""button[data-bs-target='#editModal']"", number: '2', actionColor: '#16a34a' },
                { targetSelector: "".row.g-3.mb-4 > div:nth-child(1) .card"", number: '3', actionColor: '#0284c7' },
                { targetSelector: "".row.g-3.mb-4 > div:nth-child(2) .card"", number: '4', actionColor: '#16a34a' },
                { targetSelector: "".row.g-3.mb-4 > div:nth-child(3) .card"", number: '5', actionColor: '#ca8a04' },
                { targetSelector: "".row.g-3.mb-4 > div:nth-child(4) .card"", number: '6', actionColor: '#9333ea' },
                { targetSelector: ""table.table tbody tr:first-child"", number: '7', actionColor: '#0d9488' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-recurring';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "19-recurring-transactions-screen-annotated.png"), overwrite: true);
    }
}
