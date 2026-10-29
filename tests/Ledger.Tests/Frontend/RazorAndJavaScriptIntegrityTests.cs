using System.Diagnostics;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace Ledger.Tests.Frontend;

public partial class RazorAndJavaScriptIntegrityTests
{
    private readonly string _projectDir;

    public RazorAndJavaScriptIntegrityTests()
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

    [Fact]
    public void ScanCshtmlFiles_WhenInspected_DoesNotContainUnescapedLocalizerInJavaScript()
    {
        // Interdit l'anti-pattern '@Localizer["..."]' directement dans du code JS,
        // car si la traduction contient une apostrophe (ex: "l'opération"), la syntaxe JS est cassée.
        var cshtmlFiles = Directory.GetFiles(_projectDir, "*.cshtml", SearchOption.AllDirectories);
        var violations = new List<string>();

        var unsafeLocalizerRegex = UnsafeLocalizerRegex();

        foreach (var file in cshtmlFiles)
        {
            var content = File.ReadAllText(file);
            var scriptMatches = ScriptTagsRegex().Matches(content);

            foreach (Match scriptMatch in scriptMatches)
            {
                var scriptContent = scriptMatch.Groups[1].Value;
                var matches = unsafeLocalizerRegex.Matches(scriptContent);
                foreach (Match m in matches)
                {
                    violations.Add($"{Path.GetFileName(file)}: Pattern dangereux trouvé '{m.Value}'. Utilisez JsonSerializer.Serialize(...) ou des data-attributes à la place.");
                }
            }
        }

        violations.Should().BeEmpty(
            "L'injection directe de @Localizer[...] dans des chaînes JS avec apostrophes casse l'analyseur JavaScript du navigateur. Utilisez JsonSerializer.Serialize(...)");
    }

    [Fact]
    public void ScanCshtmlFiles_WhenInspected_DoesNotContainRawModelStringsInInlineEventHandlers()
    {
        // Interdit onclick="... '@t.Label' ..." car si le libellé de l'opération contient une apostrophe,
        // l'attribut HTML et le code JS deviennent invalides.
        var cshtmlFiles = Directory.GetFiles(_projectDir, "*.cshtml", SearchOption.AllDirectories);
        var violations = new List<string>();

        var unsafeOnclickRegex = UnsafeOnclickRegex();

        foreach (var file in cshtmlFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (unsafeOnclickRegex.IsMatch(lines[i]))
                {
                    violations.Add($"{Path.GetFileName(file)}:Ligne {i + 1} : Injection directe de libellé dans onclick. Utilisez des attributs data-* (ex: data-tx-label).");
                }
            }
        }

        violations.Should().BeEmpty(
            "Les libellés bancaires contiennent souvent des apostrophes ou guillemets. Utilisez des data-attributes plutôt que des paramètres onclick inline.");
    }

    [Fact]
    public void ValidateScripts_WhenCheckedWithNode_HasNoSyntaxErrors()
    {
        var cshtmlFiles = Directory.GetFiles(_projectDir, "*.cshtml", SearchOption.AllDirectories);
        var violations = new List<string>();

        foreach (var file in cshtmlFiles)
        {
            var content = File.ReadAllText(file);
            var scriptMatches = ScriptTagsRegex().Matches(content);

            for (int sIdx = 0; sIdx < scriptMatches.Count; sIdx++)
            {
                var scriptContent = scriptMatches[sIdx].Groups[1].Value;
                if (string.IsNullOrWhiteSpace(scriptContent)) continue;

                // Neutraliser les interpolations Razor dans le JS pour que Node puisse valider la syntaxe du JS pur
                var sanitized = RazorHtmlRawRegex().Replace(scriptContent, "[];");
                sanitized = RazorSystemTextRegex().Replace(sanitized, "\"\";");
                sanitized = RazorLocalizerRegex().Replace(sanitized, "\"\"");
                sanitized = RazorIdentifierRegex().Replace(sanitized, "_razor");

                var tempFile = Path.Combine(Path.GetTempPath(), $"ledger_check_{Guid.NewGuid():N}.js");
                try
                {
                    File.WriteAllText(tempFile, sanitized);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "node",
                        Arguments = $"--check \"{tempFile}\"",
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        proc.WaitForExit(5000);
                        if (proc.ExitCode != 0)
                        {
                            var err = proc.StandardError.ReadToEnd();
                            // Détecte les vraies erreurs de syntaxe structurelles (fonction orpheline, accolade manquante, parenthèse fermante en trop)
                            if (err.Contains("SyntaxError") &&
                                (err.Contains("Unexpected end of input") ||
                                 err.Contains("Unexpected token '}'") ||
                                 err.Contains("missing ) after argument list") ||
                                 err.Contains("Unexpected token '{'")))
                            {
                                violations.Add($"{Path.GetFileName(file)} (bloc script #{sIdx + 1}) :\n{err.Trim()}\n");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Node non disponible : {ex.Message}");
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        try
                        {
                            File.Delete(tempFile);
                        }
                        catch (IOException)
                        {
                            // Node peut encore tenir le fichier ouvert à la fin du test ; le fichier reste dans %TEMP%,
                            // purgé par le système, et n'influence pas le résultat de la vérification.
                        }
                    }
                }
            }
        }

        violations.Should().BeEmpty("Tous les blocs <script> des pages Razor doivent avoir des fonctions et accolades correctement fermées.");
    }

    [Fact]
    public void ValidateStandaloneJs_WhenCheckedWithNode_IsValid()
    {
        var jsDir = Path.Combine(_projectDir, "wwwroot", "js");
        if (!Directory.Exists(jsDir)) return;

        var jsFiles = Directory.GetFiles(jsDir, "*.js", SearchOption.AllDirectories);
        var failedFiles = new List<string>();

        foreach (var file in jsFiles)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = $"--check \"{file}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(5000);
                    if (proc.ExitCode != 0)
                    {
                        var err = proc.StandardError.ReadToEnd();
                        failedFiles.Add($"{Path.GetFileName(file)}: Erreur de syntaxe JS : {err}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Si node n'est pas sur le path sur la machine, ne pas bloquer
                Debug.WriteLine($"Node non disponible : {ex.Message}");
            }
        }

        failedFiles.Should().BeEmpty("Tous les fichiers JavaScript de wwwroot/js doivent être syntaxiquement valides.");
    }

    [GeneratedRegex(@"['""]@Localizer\[[^\]]+\]['""]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UnsafeLocalizerRegex();

    [GeneratedRegex(@"<script\b[^>]*>(.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ScriptTagsRegex();

    [GeneratedRegex(@"onclick\s*=\s*""[^""]*'(?:@\w+\.Label|@\w+\.Name)'[^""]*""", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UnsafeOnclickRegex();

    [GeneratedRegex(@"@Html\.Raw\([\s\S]*?\);", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RazorHtmlRawRegex();

    [GeneratedRegex(@"@System\.Text\.[^;\n]*;", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RazorSystemTextRegex();

    [GeneratedRegex(@"@Localizer\[[^\]]*\]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RazorLocalizerRegex();

    [GeneratedRegex(@"@\w+(\.\w+)*(?:\([^)]*\))?", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RazorIdentifierRegex();
}
