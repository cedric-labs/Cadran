using System.Globalization;
using AwesomeAssertions;
using Ledger.Infrastructure.Configuration;
using Xunit;

namespace Ledger.Tests.Localization;

public class JsonStringLocalizerTests : IDisposable
{
    private readonly string _tempDir;

    public JsonStringLocalizerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ledger_loc_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Fichier fr.json
        var frJson = @"{
  ""Common"": {
    ""Save"": ""Enregistrer"",
    ""Cancel"": ""Annuler"",
    ""Welcome"": ""Bienvenue {0} !""
  },
  ""OnlyFr"": ""Seulement en français""
}";
        File.WriteAllText(Path.Combine(_tempDir, "fr.json"), frJson);

        // Fichier en.json
        var enJson = @"{
  ""Common"": {
    ""Save"": ""Save"",
    ""Cancel"": ""Cancel"",
    ""Welcome"": ""Welcome {0}!""
  },
  ""OnlyEn"": ""Only in English""
}";
        File.WriteAllText(Path.Combine(_tempDir, "en.json"), enJson);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void Localizer_InFrench_ShouldReturnFrenchValue()
    {
        var prevCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
            var localizer = new JsonStringLocalizer(_tempDir);

            var result = localizer["Common.Save"];

            result.Value.Should().Be("Enregistrer");
            result.ResourceNotFound.Should().BeFalse();
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevCulture;
        }
    }

    [Fact]
    public void Localizer_InEnglish_ShouldReturnEnglishValue()
    {
        var prevCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            var localizer = new JsonStringLocalizer(_tempDir);

            var result = localizer["Common.Save"];

            result.Value.Should().Be("Save");
            result.ResourceNotFound.Should().BeFalse();
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevCulture;
        }
    }

    [Fact]
    public void Localizer_WithFormatArguments_ShouldFormatCorrectly()
    {
        var prevCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
            var localizer = new JsonStringLocalizer(_tempDir);

            var result = localizer["Common.Welcome", "Alice"];

            result.Value.Should().Be("Bienvenue Alice !");
            result.ResourceNotFound.Should().BeFalse();
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevCulture;
        }
    }

    [Fact]
    public void Localizer_WhenKeyMissingInEnglish_ShouldFallbackToFrench()
    {
        var prevCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            var localizer = new JsonStringLocalizer(_tempDir);

            var result = localizer["OnlyFr"];

            result.Value.Should().Be("Seulement en français");
            result.ResourceNotFound.Should().BeFalse();
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevCulture;
        }
    }

    [Fact]
    public void Localizer_WhenKeyNotFoundAnywhere_ShouldReturnKeyName()
    {
        var prevCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
            var localizer = new JsonStringLocalizer(_tempDir);

            var result = localizer["NonExistent.Key"];

            result.Value.Should().Be("NonExistent.Key");
            result.ResourceNotFound.Should().BeTrue();
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevCulture;
        }
    }

    [Fact]
    public void GetAllStrings_WhenCalled_ReturnsAllCurrentCultureStrings()
    {
        var prevCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
            var localizer = new JsonStringLocalizer(_tempDir);

            var all = localizer.GetAllStrings(false);

            all.Should().Contain(s => s.Name == "Common.Save" && s.Value == "Enregistrer");
            all.Should().Contain(s => s.Name == "OnlyFr" && s.Value == "Seulement en français");
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevCulture;
        }
    }

    [Fact]
    public void Factory_Create_ShouldReturnLocalizerInstance()
    {
        var factory = new JsonStringLocalizerFactory(_tempDir);

        var locFromType = factory.Create(typeof(JsonStringLocalizerTests));
        var locFromName = factory.Create("Common", "Cadran");

        locFromType.Should().NotBeNull();
        locFromName.Should().NotBeNull();
        locFromType["Common.Save"].Value.Should().NotBeEmpty();
    }

    [Fact]
    public void GetString_InThirdLanguageWhenMissingInFrench_FallsBackToEnglish()
    {
        var prevCulture = CultureInfo.CurrentUICulture;
        try
        {
            // Culture allemande : OnlyEn n'est pas dans de ni dans fr, mais est dans en
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
            var localizer = new JsonStringLocalizer(_tempDir);

            var result = localizer["OnlyEn"];
            result.Value.Should().Be("Only in English");
            result.ResourceNotFound.Should().BeFalse();
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevCulture;
        }
    }

    [Fact]
    public void Localizer_WithMissingKeyAndArguments_FormatsKeyNameWithArgs()
    {
        var localizer = new JsonStringLocalizer(_tempDir);
        var result = localizer["MissingFormat {0} and {1}", "valA", 42];

        result.ResourceNotFound.Should().BeTrue();
        result.Value.Should().Be("MissingFormat valA and 42");
    }

    [Fact]
    public void Localizer_WithNumbersBooleansAndMalformedJson_HandlesGracefully()
    {
        // 1. JSON avec types non-string (nombres, booléens)
        var jsonWithNumbers = @"{
            ""App"": {
                ""MaxItems"": 50,
                ""IsActive"": true
            }
        }";
        File.WriteAllText(Path.Combine(_tempDir, "it.json"), jsonWithNumbers);

        var prevCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("it-IT");
            var localizer = new JsonStringLocalizer(_tempDir);

            localizer["App.MaxItems"].Value.Should().Be("50");
            localizer["App.IsActive"].Value.Should().Be("True");

            // 2. Fichier JSON malformé pour la culture 'nl'
            File.WriteAllText(Path.Combine(_tempDir, "nl.json"), "{ invalid json ");
            CultureInfo.CurrentUICulture = new CultureInfo("nl-NL");
            var localizerNl = new JsonStringLocalizer(_tempDir);

            // Doit retomber sur le fallback français ou la clé sans lever d'exception
            localizerNl["Common.Save"].Value.Should().Be("Enregistrer");
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevCulture;
        }
    }
}
