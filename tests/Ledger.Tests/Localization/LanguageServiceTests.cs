using AwesomeAssertions;
using Ledger.Infrastructure.Configuration;
using Xunit;

namespace Ledger.Tests.Localization;

public class LanguageServiceTests
{
    private static readonly string[] ExpectedLangCodes = ["fr", "en", "es", "de"];

    [Fact]
    public void GetSupportedLanguages_WithValidLocalesDirectory_DiscoversLanguages()
    {
        // Crée un répertoire temporaire avec des fichiers de locales
        var tempDir = Path.Combine(Path.GetTempPath(), "LedgerLocalesTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "fr.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "en.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "es.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "de.json"), "{}");

            var service = new LanguageService(tempDir);
            var languages = service.GetSupportedLanguages();

            languages.Should().HaveCount(4);
            languages.Select(l => l.Code).Should().Contain(ExpectedLangCodes);

            service.IsSupported("es").Should().BeTrue();
            service.IsSupported("ES").Should().BeTrue();
            service.IsSupported("de").Should().BeTrue();
            service.IsSupported("it").Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void GetSupportedLanguages_NonExistentDirectory_FallsBackToDefaults()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "LedgerNonExistent_" + Guid.NewGuid());
        var service = new LanguageService(nonExistentPath);

        var languages = service.GetSupportedLanguages();

        languages.Should().NotBeEmpty();
        languages.Select(l => l.Code).Should().Contain("fr");
    }

    [Fact]
    public void GetSupportedLanguages_AllKnownLanguages_ReturnsCorrectEmojiFlags()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LedgerAllLangs_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            var testLangs = new Dictionary<string, string>
            {
                ["fr"] = "🇫🇷",
                ["en"] = "🇬🇧",
                ["es"] = "🇪🇸",
                ["de"] = "🇩🇪",
                ["it"] = "🇮🇹",
                ["pt"] = "🇵🇹",
                ["nl"] = "🇳🇱",
                ["ja"] = "🇯🇵",
                ["zh"] = "🇨🇳",
                ["ru"] = "🇷🇺",
                ["ar"] = "🇸🇦",
                ["ko"] = "🇰🇷",
                ["pl"] = "🇵🇱",
                ["sv"] = "🇸🇪",
                ["no"] = "🇳🇴",
                ["da"] = "🇩🇰",
                ["fi"] = "🇫🇮",
                ["el"] = "🇬🇷",
                ["unknown"] = "🌐"
            };

            foreach (var code in testLangs.Keys)
            {
                File.WriteAllText(Path.Combine(tempDir, $"{code}.json"), "{}");
            }

            var service = new LanguageService(tempDir);
            var result = service.GetSupportedLanguages();

            result.Should().HaveCount(testLangs.Count);
            foreach (var (code, expectedFlag) in testLangs)
            {
                var langInfo = result.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
                langInfo.Should().NotBeNull($"Language {code} should have been discovered");
                langInfo.FlagEmoji.Should().Be(expectedFlag);
            }

            service.IsSupported(null!).Should().BeFalse();
            service.IsSupported("").Should().BeFalse();
            service.IsSupported("   ").Should().BeFalse();
            service.IsSupported("FR").Should().BeTrue();
            service.IsSupported("zh").Should().BeTrue();
            service.IsSupported("non-existent-lang").Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Vérifie que les cultures supportées conservent le symbole euro (€) et ne basculent pas
    /// sur le symbole générique international '¤' (comportement par défaut des cultures neutres en ICU / .NET 9).
    /// </summary>
    [Fact]
    public async Task ConfigureRequestLocalization_WhenExecuted_EnsuresEuroCurrencyAcrossSupportedCultures()
    {
        var supportedCultures = new List<System.Globalization.CultureInfo>
        {
            new("fr") { NumberFormat = { CurrencySymbol = "€" } },
            new("en") { NumberFormat = { CurrencySymbol = "€" } }
        };

        var options = new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
        {
            DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(supportedCultures[0]),
            SupportedCultures = supportedCultures,
            SupportedUICultures = supportedCultures
        };

        var matchedFr = options.SupportedCultures.FirstOrDefault(c => c.Name == "fr");
        matchedFr.Should().NotBeNull();
        matchedFr.NumberFormat.CurrencySymbol.Should().Be("€");

        var amount = 123.45m;
        amount.ToString("C2", matchedFr).Should().Be("123,45 €");

        var matchedEn = options.SupportedCultures.FirstOrDefault(c => c.Name == "en");
        matchedEn.Should().NotBeNull();
        matchedEn.NumberFormat.CurrencySymbol.Should().Be("€");
        amount.ToString("C2", matchedEn).Should().Be("€123.45");

        // Test with RequestLocalizationMiddleware
        var middleware = new Microsoft.AspNetCore.Localization.RequestLocalizationMiddleware(
            _ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
        );

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        httpContext.Request.Headers.AcceptLanguage = "fr-FR,fr;q=0.9";

        await middleware.Invoke(httpContext);

        var feature = httpContext.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>();
        feature.Should().NotBeNull();
        feature.RequestCulture.Culture.NumberFormat.CurrencySymbol.Should().Be("€");

        // Test with Cookie for "en"
        var httpContextEn = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        httpContextEn.Request.Headers.Cookie = $"{Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName}=c%3Den%7Cuic%3Den";
        await middleware.Invoke(httpContextEn);

        var featureEn = httpContextEn.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>();
        featureEn.Should().NotBeNull();
        featureEn.RequestCulture.Culture.NumberFormat.CurrencySymbol.Should().Be("€");
        123.45m.ToString("C2", featureEn.RequestCulture.Culture).Should().Be("€123.45");
    }
}
