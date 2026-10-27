using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Categories.Services;
using Ledger.Features.Merchants.Entities;
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
/// Generates real, annotated screenshots for Batch 3 (Transaction Operations & Splitting).
/// Strict end-user actionable approach: zero technical jargon, clean ring + numbered pins, 100% in English.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class DocumentationScreenshotLot3GeneratorTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    private static readonly string ArtifactsDirectory = @"C:\Users\user\.gemini\antigravity\brain\bdc279ef-1c47-4c3b-8899-6796af37fe8a";

    private string ProjectDocsDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/images/screens"));

    [Fact]
    public async Task GenerateLot3Screenshots_WhenExecuted_CapturesAllScreensInEnglish()
    {
        Directory.CreateDirectory(ProjectDocsDir);
        Directory.CreateDirectory(ArtifactsDirectory);

        var keyHolder = WebFixture.KestrelServices.GetRequiredService<IDatabaseKeyHolder>();
        keyHolder.SetKey(null, isEncrypted: false);

        // 1. Initialiser le compte utilisateur et ses opérations bancaires réalistes
        var (userId, checkingAccId, splitTxId, regularTxId) = await SeedUserDataAndTransactionsAsync();

        // 2. Se connecter dans Playwright avec cet utilisateur et activer la langue anglaise
        await LoginAndSetEnglishAsync();

        // 3. Écran 12 - Global Transactions Ledger (/Transactions)
        await CaptureTransactionsLedgerScreenAsync();

        // 4. Écran 13 - Edit Operation Modal (categoryModal)
        await CaptureEditTransactionModalAsync(regularTxId);

        // 5. Écran 14 - Split Transaction Modal (splitTransactionModal)
        await CaptureSplitTransactionModalAsync(splitTxId);

        // 6. Écran 15 - Batch Actions Bar (bulkActionsBar)
        await CaptureBatchActionsScreenAsync();
    }

    private async Task<(string UserId, Guid CheckingAccountId, Guid SplitTxId, Guid RegularTxId)> SeedUserDataAndTransactionsAsync()
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
                FullName = "Sarah Connor",
                EmailConfirmed = true
            };
            var res = await userManager.CreateAsync(user, "Password123");
            res.Succeeded.Should().BeTrue();
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
        var checkingType = types.FirstOrDefault(t => t.Code == "Checking");

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
                Iban = "FR76 0253 1000 2000 3000 4000 123",
                OfxAccountId = "BOURSO-CHK-01"
            };
            db.BankAccounts.Add(checkingAcc);
            await db.SaveChangesAsync();
        }

        // Marchands
        var carrefour = await db.Merchants.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.UserId == userId && m.Name == "Carrefour Market");
        if (carrefour == null)
        {
            carrefour = new Merchant { UserId = userId, Name = "Carrefour Market", NormalizedName = "CARREFOUR MARKET" };
            db.Merchants.Add(carrefour);
        }
        var edf = await db.Merchants.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.UserId == userId && m.Name == "EDF Energy");
        if (edf == null)
        {
            edf = new Merchant { UserId = userId, Name = "EDF Energy", NormalizedName = "EDF ENERGY" };
            db.Merchants.Add(edf);
        }
        await db.SaveChangesAsync();

        // Récupérer catégories & statuts
        var categories = await db.Categories.IgnoreQueryFilters().Where(c => c.UserId == userId).ToListAsync();
        var catSalary = categories.FirstOrDefault(c => c.Name.Contains("Salaire") || c.Name.Contains("Salary") || c.IsIncome);
        var catHousing = categories.FirstOrDefault(c => c.Name.Contains("Logement") || c.Name.Contains("Housing"));
        var catFood = categories.FirstOrDefault(c => c.Name.Contains("Alimentation") || c.Name.Contains("Food") || c.Name.Contains("Courses"));
        var catHome = categories.FirstOrDefault(c => c.Name.Contains("Maison") || c.Name.Contains("Home") || c.Name.Contains("Entretien"));
        var catUtilities = categories.FirstOrDefault(c => c.Name.Contains("Énergie") || c.Name.Contains("Utilities") || c.Name.Contains("Factures"));
        var catTransport = categories.FirstOrDefault(c => c.Name.Contains("Transport") || c.Name.Contains("Voyages"));
        var catSubscriptions = categories.FirstOrDefault(c => c.Name.Contains("Abonnements") || c.Name.Contains("Subscriptions"));
        var catHealth = categories.FirstOrDefault(c => c.Name.Contains("Santé") || c.Name.Contains("Health"));

        var statuses = await db.TransactionStatuses.IgnoreQueryFilters().Where(s => s.UserId == userId).ToListAsync();
        var stReconciled = statuses.FirstOrDefault(s => s.Code == "Reconciled") ?? statuses.FirstOrDefault();
        var stCleared = statuses.FirstOrDefault(s => s.Code == "Cleared") ?? statuses.FirstOrDefault();
        var stPending = statuses.FirstOrDefault(s => s.Code == "Pending") ?? statuses.FirstOrDefault();

        // Nettoyer les transactions antérieures pour une reproductibilité absolue
        var existingTx = await db.Transactions.IgnoreQueryFilters().Where(t => t.UserId == userId).ToListAsync();
        if (existingTx.Count > 0)
        {
            var txIds = existingTx.Select(t => t.Id).ToList();
            var existingSplits = await db.TransactionSplits.IgnoreQueryFilters().Where(s => txIds.Contains(s.TransactionId)).ToListAsync();
            db.TransactionSplits.RemoveRange(existingSplits);
            db.Transactions.RemoveRange(existingTx);
            await db.SaveChangesAsync();
        }

        var now = DateTime.Today;

        var salaryTx = new Transaction
        {
            UserId = userId,
            BankAccountId = checkingAcc.Id,
            Date = now.AddDays(-2),
            Label = "TechCorp Corp - Monthly Net Salary",
            Amount = 3250.00m,
            Type = TransactionType.Credit,
            Status = TransactionStatus.Reconciled,
            CustomStatusId = stReconciled?.Id,
            CategoryId = catSalary?.Id,
            Notes = "Direct deposit payroll transfer"
        };
        db.Transactions.Add(salaryTx);

        var rentTx = new Transaction
        {
            UserId = userId,
            BankAccountId = checkingAcc.Id,
            Date = now.AddDays(-4),
            Label = "Immobilier Paris - Apartment Monthly Rent",
            Amount = 950.00m,
            Type = TransactionType.Debit,
            Status = TransactionStatus.Reconciled,
            CustomStatusId = stReconciled?.Id,
            CategoryId = catHousing?.Id,
            Notes = "Monthly housing lease"
        };
        db.Transactions.Add(rentTx);

        var splitTx = new Transaction
        {
            UserId = userId,
            BankAccountId = checkingAcc.Id,
            Date = now.AddDays(-6),
            Label = "Carrefour Hypermarket - Groceries & Home Care",
            Amount = 142.30m,
            Type = TransactionType.Debit,
            Status = TransactionStatus.Cleared,
            CustomStatusId = stCleared?.Id,
            MerchantId = carrefour.Id,
            IsSplit = true
        };
        db.Transactions.Add(splitTx);
        await db.SaveChangesAsync();

        // Ajouter les deux parts ventilées
        var splitPart1 = new TransactionSplit
        {
            UserId = userId,
            TransactionId = splitTx.Id,
            Amount = 100.00m,
            CategoryId = catFood?.Id ?? categories.First().Id,
            MerchantId = carrefour.Id,
            Notes = "Fresh groceries & vegetables"
        };
        var splitPart2 = new TransactionSplit
        {
            UserId = userId,
            TransactionId = splitTx.Id,
            Amount = 42.30m,
            CategoryId = catHome?.Id ?? catFood?.Id ?? categories.First().Id,
            MerchantId = carrefour.Id,
            Notes = "Cleaning supplies & household items"
        };
        db.TransactionSplits.AddRange(splitPart1, splitPart2);

        var regularTx = new Transaction
        {
            UserId = userId,
            BankAccountId = checkingAcc.Id,
            Date = now.AddDays(-8),
            Label = "EDF Energy - Electricity Bill",
            Amount = 86.50m,
            Type = TransactionType.Debit,
            Status = TransactionStatus.Reconciled,
            CustomStatusId = stReconciled?.Id,
            CategoryId = catUtilities?.Id,
            MerchantId = edf.Id,
            Notes = "Winter heating electricity adjustment"
        };
        db.Transactions.Add(regularTx);

        var internetTx = new Transaction
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
        };
        db.Transactions.Add(internetTx);

        var trainTx = new Transaction
        {
            UserId = userId,
            BankAccountId = checkingAcc.Id,
            Date = now.AddDays(-12),
            Label = "SNCF Connect - TGV Train Travel",
            Amount = 84.00m,
            Type = TransactionType.Debit,
            Status = TransactionStatus.Cleared,
            CustomStatusId = stCleared?.Id,
            CategoryId = catTransport?.Id
        };
        db.Transactions.Add(trainTx);

        var healthTx = new Transaction
        {
            UserId = userId,
            BankAccountId = checkingAcc.Id,
            Date = now.AddDays(-14),
            Label = "City Pharmacy - Health Prescription",
            Amount = 24.60m,
            Type = TransactionType.Debit,
            Status = TransactionStatus.Pending,
            CustomStatusId = stPending?.Id,
            CategoryId = catHealth?.Id
        };
        db.Transactions.Add(healthTx);

        await db.SaveChangesAsync();

        return (userId, checkingAcc.Id, splitTx.Id, regularTx.Id);
    }

    private async Task LoginAndSetEnglishAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/Security/Login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.FillAsync("input[name='Input.Email']", "sarah.connor@cadran.local");
        await Page.FillAsync("input[name='Input.Password']", "Password123");
        await Page.ClickAsync("button[type='submit']");
        await Page.WaitForURLAsync("**/Accounts*");

        // Bascule en langue anglaise
        await Page.GotoAsync($"{BaseUrl}/SetLanguage?culture=en&returnUrl=%2FTransactions", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    private async Task CaptureTransactionsLedgerScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await Page.GotoAsync($"{BaseUrl}/Transactions", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var table = Page.Locator("#transactionsTable");
        (await table.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "12-transactions-ledger-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "12-transactions-ledger-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "12-transactions-ledger-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""#searchTermInput"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""#periodSelect"", number: '2', actionColor: '#0284c7' },
                { targetSelector: ""#btnToggleAdvancedFilters"", number: '3', actionColor: '#475569' },
                { targetSelector: ""#transactionsTable tbody tr:nth-child(2) td[data-col-key='category'] button"", number: '4', actionColor: '#16a34a' },
                { targetSelector: ""#transactionsTable tbody tr:nth-child(2) td[data-col-key='status'] button"", number: '5', actionColor: '#0d9488' },
                { targetSelector: ""#transactionsTable tbody tr:nth-child(3) button.btn-split-tx"", number: '6', actionColor: '#ca8a04' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-transactions-ledger';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "12-transactions-ledger-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureEditTransactionModalAsync(Guid regularTxId)
    {
        await Page.SetViewportSizeAsync(1440, 950);
        await Page.GotoAsync($"{BaseUrl}/Transactions", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Déclencher le modal d'édition sur l'opération EDF Energy
        await Page.EvaluateAsync(@"(id) => {
            if (typeof openCategoryModal === 'function') {
                openCategoryModal(id);
            }
        }", regularTxId.ToString());

        var modal = Page.Locator("#categoryModal");
        await modal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await Page.WaitForTimeoutAsync(500);

        var cleanPath = Path.Combine(ProjectDocsDir, "13-edit-transaction-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "13-edit-transaction-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "13-edit-transaction-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""#modalMerchantSearch"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""#modalTransactionNotes"", number: '2', actionColor: '#ca8a04' },
                { targetSelector: ""#categoryTreeSearch"", number: '3', actionColor: '#16a34a' },
                { targetSelector: ""#modalBudgetDateInput"", number: '4', actionColor: '#0284c7' },
                { targetSelector: ""#modalTransactionStatus"", number: '5', actionColor: '#0d9488' },
                { targetSelector: ""#modalApplySimilar"", number: '6', actionColor: '#9333ea' },
                { targetSelector: ""#modalSubmitBtn"", number: '7', actionColor: '#16a34a' }
            ];

            const modalDialog = document.querySelector('#categoryModal .modal-content');
            if (!modalDialog) return;

            const container = document.createElement('div');
            container.id = 'annotations-edit-tx-modal';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "13-edit-transaction-screen-annotated.png"), overwrite: true);

        // Fermer le modal
        await Page.ClickAsync("#categoryModal button.btn-close");
        await Page.WaitForTimeoutAsync(300);
    }

    private async Task CaptureSplitTransactionModalAsync(Guid splitTxId)
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await Page.GotoAsync($"{BaseUrl}/Transactions", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Ouvrir le modal de ventilation sur l'opération Carrefour
        await Page.EvaluateAsync(@"(id) => {
            if (typeof openSplitModal === 'function') {
                openSplitModal(id);
            }
        }", splitTxId.ToString());

        var modal = Page.Locator("#splitTransactionModal");
        await modal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Attendre que les lignes ventilées soient rendues par le fetch
        await Page.WaitForSelectorAsync("#splitLinesContainer tr td input.split-amount-input, #splitLinesContainer tr input");
        await Page.WaitForTimeoutAsync(400);

        var cleanPath = Path.Combine(ProjectDocsDir, "14-split-transaction-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "14-split-transaction-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "14-split-transaction-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""#splitModalTxTotalAmount"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""#splitModalRemainingBadge"", number: '2', actionColor: '#16a34a' },
                { targetSelector: ""#splitLinesContainer tr:first-child input"", number: '3', actionColor: '#ca8a04' },
                { targetSelector: ""#splitLinesContainer tr:first-child select"", number: '4', actionColor: '#0284c7' },
                { targetSelector: ""button[onclick='addSplitLine()']"", number: '5', actionColor: '#9333ea' },
                { targetSelector: ""#btnSaveSplits"", number: '6', actionColor: '#16a34a' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-split-tx-modal';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "14-split-transaction-screen-annotated.png"), overwrite: true);

        // Fermer le modal
        await Page.ClickAsync("#splitTransactionModal button.btn-close");
        await Page.WaitForTimeoutAsync(300);
    }

    private async Task CaptureBatchActionsScreenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await Page.GotoAsync($"{BaseUrl}/Transactions", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Cocher les 3 premières opérations pour activer la barre d'actions groupées
        await Page.EvaluateAsync(@"() => {
            const checkboxes = document.querySelectorAll('input.tx-select-checkbox');
            if (checkboxes.length >= 3) {
                checkboxes[0].checked = true;
                checkboxes[1].checked = true;
                checkboxes[2].checked = true;
                if (typeof updateBulkActionBar === 'function') {
                    updateBulkActionBar();
                }
            } else {
                const selectAll = document.getElementById('selectAllCheckbox');
                if (selectAll) {
                    selectAll.checked = true;
                    if (typeof toggleSelectAll === 'function') toggleSelectAll(true);
                }
            }
        }");

        await Page.WaitForSelectorAsync("#bulkActionsBar:not(.d-none)");
        await Page.WaitForTimeoutAsync(300);

        var cleanPath = Path.Combine(ProjectDocsDir, "15-batch-actions-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "15-batch-actions-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "15-batch-actions-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""#selectAllCheckbox"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""#transactionsTable tbody tr:first-child input.tx-select-checkbox"", number: '2', actionColor: '#0284c7' },
                { targetSelector: ""#selectedCountBadge"", number: '3', actionColor: '#16a34a' },
                { targetSelector: ""#bulkActionsBar button[onclick^='applyBulkStatus']:first-of-type"", number: '4', actionColor: '#0d9488' },
                { targetSelector: ""#bulkActionsBar button[onclick^='clearSelection']"", number: '5', actionColor: '#ca8a04' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-batch-actions';
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
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "15-batch-actions-screen-annotated.png"), overwrite: true);
    }
}
