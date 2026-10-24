using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Ledger.Tests.Localization;

public partial class LocalizationParityAndIntegrityTests
{
    private readonly string _projectDir;

    public LocalizationParityAndIntegrityTests()
    {
        _projectDir = FindProjectDir();
    }

    private static string FindProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var target = Path.Combine(dir.FullName, "Ledger");
            if (Directory.Exists(target) && File.Exists(Path.Combine(target, "Ledger.csproj")))
            {
                return target;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Impossible de localiser le répertoire du projet Ledger.");
    }

    private static Dictionary<string, string> LoadFlattenedLocale(string filePath)
    {
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FlattenElement(doc.RootElement, string.Empty, result);
        return result;
    }

    private static void FlattenElement(JsonElement element, string prefix, Dictionary<string, string> target)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var prop in element.EnumerateObject())
        {
            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                FlattenElement(prop.Value, key, target);
            }
            else if (prop.Value.ValueKind == JsonValueKind.String)
            {
                target[key] = prop.Value.GetString() ?? string.Empty;
            }
        }
    }

    [Fact]
    public void FrenchAndEnglishLocaleFiles_ShouldHaveExactKeyParity_AndNonEmptyValues()
    {
        var frPath = Path.Combine(_projectDir, "Resources", "Locales", "fr.json");
        var enPath = Path.Combine(_projectDir, "Resources", "Locales", "en.json");

        File.Exists(frPath).Should().BeTrue("fr.json doit exister dans Resources/Locales");
        File.Exists(enPath).Should().BeTrue("en.json doit exister dans Resources/Locales");

        var frDict = LoadFlattenedLocale(frPath);
        var enDict = LoadFlattenedLocale(enPath);

        var missingInEn = frDict.Keys.Except(enDict.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        var missingInFr = enDict.Keys.Except(frDict.Keys, StringComparer.OrdinalIgnoreCase).ToList();

        missingInEn.Should().BeEmpty("toute clé présente dans fr.json doit également être traduite dans en.json");
        missingInFr.Should().BeEmpty("toute clé présente dans en.json doit également être traduite dans fr.json");

        var emptyInFr = frDict.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList();
        var emptyInEn = enDict.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList();

        emptyInFr.Should().BeEmpty("aucune traduction dans fr.json ne doit être vide");
        emptyInEn.Should().BeEmpty("aucune traduction dans en.json ne doit être vide");
    }

    [Fact]
    public void ScanLocalizerKeys_WhenComparedToLocales_KeysExistInFrenchAndEnglish()
    {
        var frPath = Path.Combine(_projectDir, "Resources", "Locales", "fr.json");
        var enPath = Path.Combine(_projectDir, "Resources", "Locales", "en.json");

        var frDict = LoadFlattenedLocale(frPath);
        var enDict = LoadFlattenedLocale(enPath);

        var missingFrUsages = new List<string>();
        var missingEnUsages = new List<string>();

        var files = Directory.EnumerateFiles(_projectDir, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                         f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var matches = LocalizerKeyUsageRegex().Matches(content);
            var relativeFile = Path.GetRelativePath(_projectDir, file);

            foreach (Match match in matches)
            {
                var key = match.Groups["key"].Value;
                if (!frDict.ContainsKey(key))
                {
                    missingFrUsages.Add($"{relativeFile}: '{key}' absent de fr.json");
                }
                if (!enDict.ContainsKey(key))
                {
                    missingEnUsages.Add($"{relativeFile}: '{key}' absent de en.json");
                }
            }
        }

        missingFrUsages.Should().BeEmpty("chaque clé Localizer[\"...*\"] utilisée dans le code ou les vues doit exister dans fr.json");
        missingEnUsages.Should().BeEmpty("chaque clé Localizer[\"...*\"] utilisée dans le code ou les vues doit exister dans en.json");
    }

    [GeneratedRegex(@"(?:_localizer|Localizer)\[""(?<key>[^""{}$]+)""\]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LocalizerKeyUsageRegex();
}
