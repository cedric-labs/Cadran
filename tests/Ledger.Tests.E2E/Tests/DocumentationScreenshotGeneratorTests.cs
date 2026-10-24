using FluentAssertions;
using Ledger.Features.Security.Context;
using Ledger.Tests.E2E.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Ledger.Tests.E2E.Tests;

/// <summary>
/// Génère les captures d'écran réelles et annotées pour la documentation utilisateur (Lot 1 - Premier Lancement & Sécurité).
/// Approche 100 % actionnable grand public (zéro jargon, zéro élément décoratif).
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class DocumentationScreenshotGeneratorTests(AppWebTestFixture webFixture, PlaywrightFixture playwrightFixture)
    : E2ETestBase(webFixture, playwrightFixture)
{
    private static readonly string ArtifactsDirectory = @"C:\Users\user\.gemini\antigravity\brain\bdc279ef-1c47-4c3b-8899-6796af37fe8a";

    private string ProjectDocsDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/images/screens"));

    [Fact]
    public async Task GenerateLot1Screenshots_WhenExecuted_CapturesAllScreensInEnglish()
    {
        Directory.CreateDirectory(ProjectDocsDir);
        Directory.CreateDirectory(ArtifactsDirectory);

        // 1. Écran 01 - Welcome Landing Page (1er Lancement)
        await CaptureWelcomeScreenAsync();

        // 2. Écran 02 - Inscription (Register)
        await CaptureRegisterScreenAsync();

        // 3. Écran 03 - Connexion (Login)
        await CaptureLoginScreenAsync();

        // 4. Écran 04 - Chiffrement Base de Données (Database Encryption)
        await CaptureDatabaseEncryptionScreenAsync();

        // 5. Écran 05 - Déverrouillage Base de Données (Cold-Boot Unlock)
        await CaptureUnlockDatabaseScreenAsync();
    }

    private async Task CaptureWelcomeScreenAsync()
    {
        await Page.SetViewportSizeAsync(1280, 780);
        await Page.GotoAsync($"{BaseUrl}/SetLanguage?culture=en&returnUrl=%2F", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var getStartedBtn = Page.Locator("a.btn-lg[href='/Security/Register']");
        (await getStartedBtn.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "01-welcome-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "01-welcome-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "01-welcome-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""#languageDropdown, .dropdown button, .dropdown-toggle"", number: '1', actionColor: '#6366f1' },
                { targetSelector: ""a.btn-lg[href='/Security/Register']"", number: '2', actionColor: '#16a34a' },
                { targetSelector: ""a.btn-lg[href='/Security/Login']"", number: '3', actionColor: '#2563eb' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-welcome';
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
                ring.style.borderRadius = '8px';
                ring.style.boxShadow = `0 0 0 3px ${item.actionColor}25`;
                container.appendChild(ring);

                const pin = document.createElement('div');
                pin.textContent = item.number;
                pin.style.position = 'absolute';
                pin.style.left = `${tRect.left - 12}px`;
                pin.style.top = `${tRect.top - 12}px`;
                pin.style.width = '24px';
                pin.style.height = '24px';
                pin.style.borderRadius = '50%';
                pin.style.backgroundColor = item.actionColor;
                pin.style.color = '#ffffff';
                pin.style.fontSize = '12px';
                pin.style.fontWeight = '700';
                pin.style.display = 'flex';
                pin.style.alignItems = 'center';
                pin.style.justifyContent = 'center';
                pin.style.border = '2px solid #ffffff';
                pin.style.boxShadow = '0 2px 6px rgba(0,0,0,0.35)';
                pin.style.zIndex = '100002';
                container.appendChild(pin);
            });
        }");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = annotatedPath, FullPage = false });
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "01-welcome-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureRegisterScreenAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/SetLanguage?culture=en&returnUrl=%2FSecurity%2FRegister", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var submitBtn = Page.Locator("button[type='submit']");
        (await submitBtn.IsVisibleAsync()).Should().BeTrue();

        var cleanPath = Path.Combine(ProjectDocsDir, "02-register-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "02-register-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "02-register-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""input[name='Input.FullName']"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""input[name='Input.Email']"", number: '2', actionColor: '#2563eb' },
                { targetSelector: ""input[name='Input.Password']"", number: '3', actionColor: '#2563eb' },
                { targetSelector: ""input[name='Input.ConfirmPassword']"", number: '4', actionColor: '#2563eb' },
                { targetSelector: ""button[type='submit']"", number: '5', actionColor: '#16a34a' },
                { targetSelector: "".card-body a[href='/Security/Login']"", number: '6', actionColor: '#d97706' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-register';
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

                const containerEl = target.closest('.input-group') || target;
                const tRect = containerEl.getBoundingClientRect();

                const ring = document.createElement('div');
                ring.style.position = 'absolute';
                ring.style.left = `${tRect.left - 3}px`;
                ring.style.top = `${tRect.top - 3}px`;
                ring.style.width = `${tRect.width + 6}px`;
                ring.style.height = `${tRect.height + 6}px`;
                ring.style.border = `2px solid ${item.actionColor}`;
                ring.style.borderRadius = '8px';
                ring.style.boxShadow = `0 0 0 3px ${item.actionColor}25`;
                container.appendChild(ring);

                const pin = document.createElement('div');
                pin.textContent = item.number;
                pin.style.position = 'absolute';
                pin.style.left = `${tRect.left - 12}px`;
                pin.style.top = `${tRect.top - 12}px`;
                pin.style.width = '24px';
                pin.style.height = '24px';
                pin.style.borderRadius = '50%';
                pin.style.backgroundColor = item.actionColor;
                pin.style.color = '#ffffff';
                pin.style.fontSize = '12px';
                pin.style.fontWeight = '700';
                pin.style.display = 'flex';
                pin.style.alignItems = 'center';
                pin.style.justifyContent = 'center';
                pin.style.border = '2px solid #ffffff';
                pin.style.boxShadow = '0 2px 6px rgba(0,0,0,0.35)';
                pin.style.zIndex = '100002';
                container.appendChild(pin);
            });
        }");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = annotatedPath, FullPage = false });
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "02-register-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureLoginScreenAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/SetLanguage?culture=en&returnUrl=%2FSecurity%2FLogin", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var cleanPath = Path.Combine(ProjectDocsDir, "03-login-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "03-login-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "03-login-screen.png"), overwrite: true);

        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""input[name='Input.Email']"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""input[name='Input.Password']"", number: '2', actionColor: '#2563eb' },
                { targetSelector: ""input[name='Input.RememberMe']"", number: '3', actionColor: '#0d9488' },
                { targetSelector: ""button[type='submit']"", number: '4', actionColor: '#16a34a' },
                { targetSelector: "".card-body a[href='/Security/Register']"", number: '5', actionColor: '#d97706' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-login';
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

                const containerEl = target.closest('.input-group') || target.closest('.form-check') || target;
                const tRect = containerEl.getBoundingClientRect();

                const ring = document.createElement('div');
                ring.style.position = 'absolute';
                ring.style.left = `${tRect.left - 3}px`;
                ring.style.top = `${tRect.top - 3}px`;
                ring.style.width = `${tRect.width + 6}px`;
                ring.style.height = `${tRect.height + 6}px`;
                ring.style.border = `2px solid ${item.actionColor}`;
                ring.style.borderRadius = '8px';
                ring.style.boxShadow = `0 0 0 3px ${item.actionColor}25`;
                container.appendChild(ring);

                const pin = document.createElement('div');
                pin.textContent = item.number;
                pin.style.position = 'absolute';
                pin.style.left = `${tRect.left - 12}px`;
                pin.style.top = `${tRect.top - 12}px`;
                pin.style.width = '24px';
                pin.style.height = '24px';
                pin.style.borderRadius = '50%';
                pin.style.backgroundColor = item.actionColor;
                pin.style.color = '#ffffff';
                pin.style.fontSize = '12px';
                pin.style.fontWeight = '700';
                pin.style.display = 'flex';
                pin.style.alignItems = 'center';
                pin.style.justifyContent = 'center';
                pin.style.border = '2px solid #ffffff';
                pin.style.boxShadow = '0 2px 6px rgba(0,0,0,0.35)';
                pin.style.zIndex = '100002';
                container.appendChild(pin);
            });
        }");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = annotatedPath, FullPage = false });
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "03-login-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureDatabaseEncryptionScreenAsync()
    {
        await Page.SetViewportSizeAsync(1280, 850);

        // 1. S'inscrire pour obtenir une session authentifiée propre
        await Page.GotoAsync($"{BaseUrl}/Security/Register");
        var emailInput = Page.Locator("input[name='Input.Email']");
        var fullNameInput = Page.Locator("input[name='Input.FullName']");
        var passwordInput = Page.Locator("input[name='Input.Password']");
        var confirmPasswordInput = Page.Locator("input[name='Input.ConfirmPassword']");

        var testEmail = $"admin_{Random.Shared.Next(100, 999)}@cadran.local";
        await fullNameInput.FillAsync("Admin");
        await emailInput.FillAsync(testEmail);
        await passwordInput.FillAsync("P@ssword123!");
        await confirmPasswordInput.FillAsync("P@ssword123!");
        await Page.Locator(".card button[type='submit']").ClickAsync();

        await Page.WaitForURLAsync("**/Accounts**");

        // 2. Navigation vers la page de chiffrement en anglais
        await Page.GotoAsync($"{BaseUrl}/SetLanguage?culture=en&returnUrl=%2FSecurity%2FDatabaseEncryption", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var cleanPath = Path.Combine(ProjectDocsDir, "04-database-encryption-screen.png");
        var annotatedPath = Path.Combine(ProjectDocsDir, "04-database-encryption-screen-annotated.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
        File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "04-database-encryption-screen.png"), overwrite: true);

        // Annoter UNIQUEMENT les 4 champs/boutons interactifs
        await Page.EvaluateAsync(@"() => {
            const items = [
                { targetSelector: ""#encryptNewPassphrase"", number: '1', actionColor: '#2563eb' },
                { targetSelector: ""#encryptConfirmPassphrase"", number: '2', actionColor: '#2563eb' },
                { targetSelector: ""#encryptRemember"", number: '3', actionColor: '#0d9488' },
                { targetSelector: "".card button[type='submit']"", number: '4', actionColor: '#16a34a' }
            ];

            const container = document.createElement('div');
            container.id = 'annotations-db-enc';
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
                ring.style.borderRadius = '8px';
                ring.style.boxShadow = `0 0 0 3px ${item.actionColor}25`;
                container.appendChild(ring);

                const pin = document.createElement('div');
                pin.textContent = item.number;
                pin.style.position = 'absolute';
                pin.style.left = `${tRect.left - 12}px`;
                pin.style.top = `${tRect.top - 12}px`;
                pin.style.width = '24px';
                pin.style.height = '24px';
                pin.style.borderRadius = '50%';
                pin.style.backgroundColor = item.actionColor;
                pin.style.color = '#ffffff';
                pin.style.fontSize = '12px';
                pin.style.fontWeight = '700';
                pin.style.display = 'flex';
                pin.style.alignItems = 'center';
                pin.style.justifyContent = 'center';
                pin.style.boxShadow = '0 2px 6px rgba(0,0,0,0.35)';
                pin.style.border = '2px solid #ffffff';
                pin.style.zIndex = '100002';
                container.appendChild(pin);
            });
        }");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = annotatedPath, FullPage = false });
        File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "04-database-encryption-screen-annotated.png"), overwrite: true);
    }

    private async Task CaptureUnlockDatabaseScreenAsync()
    {
        // Forcer le statut verrouillé temporairement pour capturer l'écran de déverrouillage
        var keyHolder = WebFixture.KestrelServices.GetRequiredService<IDatabaseKeyHolder>();
        keyHolder.SetKey(null, isEncrypted: true);

        try
        {
            await Page.GotoAsync($"{BaseUrl}/SetLanguage?culture=en&returnUrl=%2FSecurity%2FUnlockDatabase", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            var unlockInput = Page.Locator("#dbPassphraseInput");
            (await unlockInput.IsVisibleAsync()).Should().BeTrue();

            var cleanPath = Path.Combine(ProjectDocsDir, "05-database-unlock-screen.png");
            var annotatedPath = Path.Combine(ProjectDocsDir, "05-database-unlock-screen-annotated.png");

            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = cleanPath, FullPage = false });
            File.Copy(cleanPath, Path.Combine(ArtifactsDirectory, "05-database-unlock-screen.png"), overwrite: true);

            // Annoter UNIQUEMENT les 4 éléments interactifs (suppression du bouclier passif)
            await Page.EvaluateAsync(@"() => {
                const items = [
                    { targetSelector: ""#dbPassphraseInput"", number: '1', actionColor: '#2563eb' },
                    { targetSelector: ""#togglePassphraseBtn"", number: '2', actionColor: '#64748b' },
                    { targetSelector: ""#rememberOnDeviceCheck"", number: '3', actionColor: '#0d9488' },
                    { targetSelector: ""button[type='submit']"", number: '4', actionColor: '#16a34a' }
                ];

                const container = document.createElement('div');
                container.id = 'annotations-unlock';
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

                    const containerEl = target.closest('.form-check') || target;
                    const tRect = containerEl.getBoundingClientRect();

                    const ring = document.createElement('div');
                    ring.style.position = 'absolute';
                    ring.style.left = `${tRect.left - 3}px`;
                    ring.style.top = `${tRect.top - 3}px`;
                    ring.style.width = `${tRect.width + 6}px`;
                    ring.style.height = `${tRect.height + 6}px`;
                    ring.style.border = `2px solid ${item.actionColor}`;
                    ring.style.borderRadius = '8px';
                    ring.style.boxShadow = `0 0 0 3px ${item.actionColor}25`;
                    container.appendChild(ring);

                    const pin = document.createElement('div');
                    pin.textContent = item.number;
                    pin.style.position = 'absolute';
                    pin.style.left = `${tRect.left - 12}px`;
                    pin.style.top = `${tRect.top - 12}px`;
                    pin.style.width = '24px';
                    pin.style.height = '24px';
                    pin.style.borderRadius = '50%';
                    pin.style.backgroundColor = item.actionColor;
                    pin.style.color = '#ffffff';
                    pin.style.fontSize = '12px';
                    pin.style.fontWeight = '700';
                    pin.style.display = 'flex';
                    pin.style.alignItems = 'center';
                    pin.style.justifyContent = 'center';
                    pin.style.boxShadow = '0 2px 6px rgba(0,0,0,0.35)';
                    pin.style.border = '2px solid #ffffff';
                    pin.style.zIndex = '100002';
                    container.appendChild(pin);
                });
            }");

            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = annotatedPath, FullPage = false });
            File.Copy(annotatedPath, Path.Combine(ArtifactsDirectory, "05-database-unlock-screen-annotated.png"), overwrite: true);
        }
        finally
        {
            keyHolder.SetKey(null, isEncrypted: false);
        }
    }
}
