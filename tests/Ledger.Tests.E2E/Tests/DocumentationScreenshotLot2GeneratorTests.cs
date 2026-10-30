using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Categories.Services;
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
/// Generates real, annotated screenshots for Batch 2 (Dashboard & Net Worth Overview + Account Management).
/// Strict end-user actionable approach: zero technical jargon, non-overlapping callouts, 100% in English.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class DocumentationScreenshotLot2GeneratorTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    private static readonly string ArtifactsDirectory = Path.Combine(Path.GetTempPath(), "cadran-doc-screenshots");

    private static string ProjectDocsDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/images/screens"));

    [Fact]
    public async Task GenerateLot2Screenshots_WhenExecuted_CapturesAllScreensInEnglish()
    {
        Directory.CreateDirectory(ProjectDocsDir);
        Directory.CreateDirectory(ArtifactsDirectory);

        var keyHolder = WebFixture.KestrelServices.GetRequiredService<IDatabaseKeyHolder>();
        keyHolder.SetKey(null, isEncrypted: false);

        // 1. Initialiser le compte utilisateur et ses données financières de démonstration réalistes
        var (_, accountCheckingId) = await SeedUserDataAndAccountsAsync();

        // 2. Se connecter dans Playwright avec cet utilisateur et activer la langue anglaise
        await LoginAndSetEnglishAsync();

        // 3. Écran 06 - Dashboard & Net Worth Overview (/Accounts)
        await CaptureDashboardScreenAsync();

        // 4. Écran 07 - Create Bank Account (/Accounts/CreateAccount)
        await CaptureCreateAccountScreenAsync();

        // 5. Écran 08 - Edit Bank Account (/Accounts/Edit/{id})
        await CaptureEditAccountScreenAsync(accountCheckingId);

        // 6. Écran 09 - Account Register & Statement Details (/Accounts/Details/{id})
        await CaptureAccountDetailsScreenAsync(accountCheckingId);

        // 7. Écran 10 - Custom Account Types (/Accounts/Types)
        await CaptureAccountTypesScreenAsync();

        // 8. Écran 11 - Custom Transaction Statuses (/Accounts/Statuses)
        await CaptureTransactionStatusesScreenAsync();
    }

    private async Task<(string UserId, Guid CheckingAccountId)> SeedUserDataAndAccountsAsync()
    {
        using var scope = WebFixture.KestrelServices.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var accountTypeService = scope.ServiceProvider.GetRequiredService<IAccountTypeService>();
        var statusService = scope.ServiceProvider.GetRequiredService<ITransactionStatusService>();
        var categorySeedService = scope.ServiceProvider.GetRequiredService<ICategorySeedService>();

        const string email = "sarah.connor@cadran.local";
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "Sarah Connor"
            };
            var result = await userManager.CreateAsync(user, "Password123");
            result.Succeeded.Should().BeTrue();
        }

        var userId = user.Id;

        // Données de référence
        await accountTypeService.EnsureDefaultAccountTypesForUserAsync(userId);
        await statusService.EnsureDefaultStatusesForUserAsync(userId);
        await categorySeedService.EnsureDefaultCategoriesForUserAsync(userId);

        // Nettoyer les comptes existants éventuels pour repartir d'un jeu propre
        var existingAccounts = await db.BankAccounts.IgnoreQueryFilters().Where(a => a.UserId == userId).ToListAsync();
        if (existingAccounts.Count > 0)
        {
            var accIds = existingAccounts.Select(a => a.Id).ToList();
            var existingTx = await db.Transactions.IgnoreQueryFilters().Where(t => accIds.Contains(t.BankAccountId)).ToListAsync();
            db.Transactions.RemoveRange(existingTx);
            db.BankAccounts.RemoveRange(existingAccounts);
            await db.SaveChangesAsync();
        }

        // Créer les banques
        var boursoBank = await db.Banks.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.UserId == userId && b.Name == "BoursoBank");
        if (boursoBank == null)
        {
            boursoBank = new Bank { UserId = userId, Name = "BoursoBank", ColorHex = "#0253a5" };
            db.Banks.Add(boursoBank);
        }

        var bnpBank = await db.Banks.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.UserId == userId && b.Name == "BNP Paribas");
        if (bnpBank == null)
        {
            bnpBank = new Bank { UserId = userId, Name = "BNP Paribas", ColorHex = "#00915a" };
            db.Banks.Add(bnpBank);
        }

        var bdBank = await db.Banks.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.UserId == userId && b.Name == "Bourse Direct");
        if (bdBank == null)
        {
            bdBank = new Bank { UserId = userId, Name = "Bourse Direct", ColorHex = "#e11d48" };
            db.Banks.Add(bdBank);
        }

        await db.SaveChangesAsync();

        var types = await db.CustomAccountTypes.IgnoreQueryFilters().Where(t => t.UserId == userId).ToListAsync();
        var checkingType = types.FirstOrDefault(t => string.Equals(t.Code, "Checking", StringComparison.Ordinal));
        var livretAType = types.FirstOrDefault(t => string.Equals(t.Code, "LivretA", StringComparison.Ordinal));
        var cardType = types.FirstOrDefault(t => string.Equals(t.Code, "CreditCard", StringComparison.Ordinal));
        var peaType = types.FirstOrDefault(t => string.Equals(t.Code, "Pea", StringComparison.Ordinal));

        var checkingAcc = new BankAccount
        {
            UserId = userId,
            BankId = boursoBank.Id,
            Name = "Main Checking Account",
            CustomAccountTypeId = checkingType?.Id,
            Type = AccountType.Checking,
            Currency = "EUR",
            InitialBalance = 2845.50m,
            Iban = "FR76 0253 1000 2000 3000 4000 123",
            OfxAccountId = "BOURSO-CHK-01"
        };
        db.BankAccounts.Add(checkingAcc);

        var savingsAcc = new BankAccount
        {
            UserId = userId,
            BankId = bnpBank.Id,
            Name = "Livret A Savings",
            CustomAccountTypeId = livretAType?.Id,
            Type = AccountType.Savings,
            Currency = "EUR",
            InitialBalance = 14250.00m,
            Iban = "FR76 3000 4000 5000 6000 7000 890",
            OfxAccountId = "BNP-LIVRETA-02"
        };
        db.BankAccounts.Add(savingsAcc);

        var peaAcc = new BankAccount
        {
            UserId = userId,
            BankId = bdBank.Id,
            Name = "PEA Investment Portfolio",
            CustomAccountTypeId = peaType?.Id,
            Type = AccountType.Investment,
            Currency = "EUR",
            InitialBalance = 18650.00m,
            OfxAccountId = "BD-PEA-99"
        };
        db.BankAccounts.Add(peaAcc);

        var cardAcc = new BankAccount
        {
            UserId = userId,
            BankId = boursoBank.Id,
            Name = "Deferred Visa Premier",
            CustomAccountTypeId = cardType?.Id,
            Type = AccountType.CreditCard,
            Currency = "EUR",
            InitialBalance = -785.40m,
            OfxAccountId = "BOURSO-CC-77"
        };
        db.BankAccounts.Add(cardAcc);

        await db.SaveChangesAsync();

        // Récupérer catégories et statuts pour des opérations réalistes
        var categories = await db.Categories.IgnoreQueryFilters().Where(c => c.UserId == userId).ToListAsync();
        var catSalary = categories.FirstOrDefault(c => c.Name.Contains("Salaire", StringComparison.Ordinal) || c.Name.Contains("Revenus", StringComparison.Ordinal));
        var catHousing = categories.FirstOrDefault(c => c.Name.Contains("Logement", StringComparison.Ordinal) || c.Name.Contains("immobilier", StringComparison.Ordinal));
        var catFood = categories.FirstOrDefault(c => c.Name.Contains("vie courante", StringComparison.Ordinal) || c.Name.Contains("Alimentation", StringComparison.Ordinal));
        var catTransport = categories.FirstOrDefault(c => c.Name.Contains("Transports", StringComparison.Ordinal));
        var catSubscriptions = categories.FirstOrDefault(c => c.Name.Contains("Abonnements", StringComparison.Ordinal) || c.Name.Contains("Services", StringComparison.Ordinal));

        var statuses = await db.TransactionStatuses.IgnoreQueryFilters().Where(s => s.UserId == userId).ToListAsync();
        var stReconciled = statuses.FirstOrDefault(s => string.Equals(s.Code, "Reconciled", StringComparison.Ordinal));
        var stCleared = statuses.FirstOrDefault(s => string.Equals(s.Code, "Cleared", StringComparison.Ordinal));

        var now = DateTime.Today;
        var sampleTransactions = new List<Transaction>
        {
            new()
            {
                UserId = userId,
                BankAccountId = checkingAcc.Id,
                Date = now.AddDays(-2),
                Label = "Tech Solutions SAS - Monthly Salary",
                Amount = 3250.00m,
                Type = TransactionType.Credit,
                Status = TransactionStatus.Cleared,
                CustomStatusId = stCleared?.Id,
                CategoryId = catSalary?.Id,
                Reference = "VIR-SAL-OCT26"
            },
            new()
            {
                UserId = userId,
                BankAccountId = checkingAcc.Id,
                Date = now.AddDays(-4),
                Label = "Apartment Monthly Rent",
                Amount = 950.00m,
                Type = TransactionType.Debit,
                Status = TransactionStatus.Reconciled,
                CustomStatusId = stReconciled?.Id,
                CategoryId = catHousing?.Id,
                Reference = "VIR-LOYER-PARIS"
            },
            new()
            {
                UserId = userId,
                BankAccountId = checkingAcc.Id,
                Date = now.AddDays(-5),
                Label = "Organic Supermarket & Fresh Groceries",
                Amount = 142.30m,
                Type = TransactionType.Debit,
                Status = TransactionStatus.Reconciled,
                CustomStatusId = stReconciled?.Id,
                CategoryId = catFood?.Id
            },
            new()
            {
                UserId = userId,
                BankAccountId = checkingAcc.Id,
                Date = now.AddDays(-8),
                Label = "EDF Energy - Electricity Bill",
                Amount = 86.50m,
                Type = TransactionType.Debit,
                Status = TransactionStatus.Reconciled,
                CustomStatusId = stReconciled?.Id,
                CategoryId = catHousing?.Id
            },
            new()
            {
                UserId = userId,
                BankAccountId = checkingAcc.Id,
                Date = now.AddDays(-10),
                Label = "Fiber High-Speed Internet & Mobile",
                Amount = 39.99m,
                Type = TransactionType.Debit,
                Status = TransactionStatus.Reconciled,
                CustomStatusId = stReconciled?.Id,
                CategoryId = catSubscriptions?.Id
            },
            new()
            {
                UserId = userId,
                BankAccountId = checkingAcc.Id,
                Date = now.AddDays(-12),
                Label = "SNCF Connect - Train Travel",
                Amount = 84.00m,
                Type = TransactionType.Debit,
                Status = TransactionStatus.Cleared,
                CustomStatusId = stCleared?.Id,
                CategoryId = catTransport?.Id
            }
        };

        db.Transactions.AddRange(sampleTransactions);
        await db.SaveChangesAsync();

        return (userId, checkingAcc.Id);
    }

    private async Task LoginAndSetEnglishAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/Security/Login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.FillAsync("input[name='Input.Email']", "sarah.connor@cadran.local");
        await Page.FillAsync("input[name='Input.Password']", "Password123");
        await Page.ClickAsync("button[type='submit']");
        await Page.WaitForURLAsync("**/Accounts*");

        // Bascule en langue anglaise
        await Page.GotoAsync($"{BaseUrl}/SetLanguage?culture=en&returnUrl=%2FAccounts", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    private async Task CaptureDashboardScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 880);
        await Page.GotoAsync($"{BaseUrl}/Accounts", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var newAccountBtn = Page.Locator("a[href='/Accounts/CreateAccount'].btn-primary");
        (await newAccountBtn.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "06-dashboard-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "06-dashboard-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "06-dashboard-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""a[href='/Accounts/CreateAccount'].btn-primary"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""a[href='/Imports'].btn-outline-primary"", number: '2', actionColor: '#0284c7' },
                { targetSelector: ""a[href='/Transactions'].btn-outline-secondary"", number: '3', actionColor: '#475569' },
                { targetSelector: "".card .card-body a[href^='/Accounts/Details/']"", number: '4', actionColor: '#16a34a' },
                { targetSelector: "".card .card-body a[href^='/Accounts/Edit/']"", number: '5', actionColor: '#ca8a04' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-dashboard';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "06-dashboard-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureCreateAccountScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 980);
        await Page.GotoAsync($"{BaseUrl}/Accounts/CreateAccount", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var accountNameInput = Page.Locator("input[name='Input.AccountName']");
        (await accountNameInput.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "07-create-account-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "07-create-account-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "07-create-account-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""select[name='Input.ExistingBankId']"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""input[name='Input.AccountName']"", number: '2', actionColor: '#16a34a' },
                { targetSelector: ""select[name='Input.CustomAccountTypeId']"", number: '3', actionColor: '#0284c7' },
                { targetSelector: ""input[name='Input.InitialBalance']"", number: '4', actionColor: '#ca8a04' },
                { targetSelector: "".card form button[type='submit']"", number: '5', actionColor: '#16a34a' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-create-account';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "07-create-account-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureEditAccountScreenAsync(Guid checkingAccountId)
    {
        await Page.SetViewportSizeAsync(1440, 980);
        await Page.GotoAsync($"{BaseUrl}/Accounts/Edit/{checkingAccountId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var accountNameInput = Page.Locator("input[name='Input.AccountName']");
        (await accountNameInput.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "08-edit-account-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "08-edit-account-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "08-edit-account-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""input[name='Input.AccountName']"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""select[name='Input.CustomAccountTypeId']"", number: '2', actionColor: '#0284c7' },
                { targetSelector: ""input[name='Input.InitialBalance']"", number: '3', actionColor: '#ca8a04' },
                { targetSelector: ""#archivedSwitch"", number: '4', actionColor: '#9333ea' },
                { targetSelector: "".card form button[type='submit']"", number: '5', actionColor: '#16a34a' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-edit-account';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "08-edit-account-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureAccountDetailsScreenAsync(Guid checkingAccountId)
    {
        await Page.SetViewportSizeAsync(1440, 1000);
        await Page.GotoAsync($"{BaseUrl}/Accounts/Details/{checkingAccountId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var searchInput = Page.Locator("#searchTermInput");
        (await searchInput.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "09-account-details-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "09-account-details-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "09-account-details-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""#searchTermInput"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""#periodSelect"", number: '2', actionColor: '#0284c7' },
                { targetSelector: ""#filterForm button[type='submit']"", number: '3', actionColor: '#16a34a' },
                { targetSelector: ""a.btn-primary[href^='/Imports']"", number: '4', actionColor: '#9333ea' },
                { targetSelector: ""button[data-bs-target='#applyAccountRulesModal']"", number: '5', actionColor: '#16a34a' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-account-details';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "09-account-details-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureAccountTypesScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 850);
        await Page.GotoAsync($"{BaseUrl}/Accounts/Types", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var newTypeBtn = Page.Locator("button[onclick='openTypeModal()']");
        (await newTypeBtn.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "10-account-types-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "10-account-types-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "10-account-types-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""button[onclick='openTypeModal()']"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""table tbody tr:first-child td:nth-child(2) span.badge"", number: '2', actionColor: '#0284c7' },
                { targetSelector: ""table tbody tr:first-child td:nth-child(4) span.badge"", number: '3', actionColor: '#16a34a' },
                { targetSelector: ""table tbody tr:first-child button[onclick^='editType']"", number: '4', actionColor: '#ca8a04' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-account-types';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "10-account-types-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureTransactionStatusesScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 800);
        await Page.GotoAsync($"{BaseUrl}/Accounts/Statuses", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var newStatusBtn = Page.Locator("button[onclick='openStatusModal()']");
        (await newStatusBtn.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "11-account-statuses-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "11-account-statuses-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "11-account-statuses-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""button[onclick='openStatusModal()']"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""table tbody tr:first-child td:first-child span.badge"", number: '2', actionColor: '#0284c7' },
                { targetSelector: ""table tbody tr:first-child td:nth-child(3)"", number: '3', actionColor: '#16a34a' },
                { targetSelector: ""table tbody tr:first-child button[onclick^='editStatus']"", number: '4', actionColor: '#ca8a04' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-account-statuses';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "11-account-statuses-screen-annotated.png"), overwrite: true);
    }
}
