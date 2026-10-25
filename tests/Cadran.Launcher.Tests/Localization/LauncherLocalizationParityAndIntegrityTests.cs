using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Cadran.Launcher.Tests.Localization;

public sealed partial class LauncherLocalizationParityAndIntegrityTests
{
    private static readonly string[] RequiredLanguages = ["fr", "en", "es", "de"];
    private readonly string _projectDir;

    public LauncherLocalizationParityAndIntegrityTests()
    {
        _projectDir = FindProjectDir();
    }

    private static string FindProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var target = Path.Combine(dir.FullName, "Cadran.Launcher");
            if (Directory.Exists(target) && File.Exists(Path.Combine(target, "Cadran.Launcher.csproj")))
            {
                return target;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Impossible de localiser le répertoire du projet Cadran.Launcher.");
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
    public void SupportedLocaleFiles_WhenLoaded_ShouldHaveExactKeyParityAndNonEmptyValues()
    {
        var localesDir = Path.Combine(_projectDir, "Resources", "Locales");
        Directory.Exists(localesDir).Should().BeTrue("le répertoire Resources/Locales doit exister");

        var localeDicts = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var lang in RequiredLanguages)
        {
            var filePath = Path.Combine(localesDir, $"{lang}.json");
            File.Exists(filePath).Should().BeTrue($"{lang}.json doit exister dans Resources/Locales");
            localeDicts[lang] = LoadFlattenedLocale(filePath);
        }

        var frDict = localeDicts["fr"];

        foreach (var lang in RequiredLanguages)
        {
            var dict = localeDicts[lang];

            var missingKeys = frDict.Keys.Except(dict.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            missingKeys.Should().BeEmpty($"toute clé présente dans fr.json doit être traduite dans {lang}.json");

            var extraKeys = dict.Keys.Except(frDict.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            extraKeys.Should().BeEmpty($"aucune clé superflue ne doit exister dans {lang}.json");

            var emptyValues = dict.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList();
            emptyValues.Should().BeEmpty($"aucune traduction dans {lang}.json ne doit être vide");
        }
    }

    [Fact]
    public void ScanLocalizerKeys_WhenComparedToLocales_KeysExistInAllSupportedLanguages()
    {
        var localesDir = Path.Combine(_projectDir, "Resources", "Locales");
        var localeDicts = RequiredLanguages.ToDictionary(
            lang => lang,
            lang => LoadFlattenedLocale(Path.Combine(localesDir, $"{lang}.json")),
            StringComparer.OrdinalIgnoreCase);

        var missingUsages = new List<string>();

        var files = Directory.EnumerateFiles(_projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var matches = LocalizerKeyUsageRegex().Matches(content);
            var relativeFile = Path.GetRelativePath(_projectDir, file);

            foreach (Match match in matches)
            {
                var key = match.Groups["key"].Value;
                foreach (var lang in RequiredLanguages)
                {
                    if (!localeDicts[lang].ContainsKey(key))
                    {
                        missingUsages.Add($"{relativeFile}: '{key}' absent de {lang}.json");
                    }
                }
            }
        }

        missingUsages.Should().BeEmpty("chaque clé _localizer[\"...*\"] utilisée dans le code UI du Launcher doit exister dans toutes les langues");
    }

    [GeneratedRegex(@"(?:_localizer)\[""(?<key>[^""{}$]+)""\]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LocalizerKeyUsageRegex();
}
