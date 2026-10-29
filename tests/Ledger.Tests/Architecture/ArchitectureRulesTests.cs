using System.Text.RegularExpressions;
using AwesomeAssertions;
using Ledger.Domain.Entities;
using NetArchTest.Rules;
using Xunit;

namespace Ledger.Tests.Architecture;

public partial class ArchitectureRulesTests
{
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

    private static IEnumerable<string> EnumerateSourceFiles(string projectDir, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.EnumerateFiles(projectDir, pattern, SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(projectDir, file);
                if (relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("Migrations" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("wwwroot" + Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                yield return file;
            }
        }
    }

    [Fact]
    public void Codebase_ShouldNotContain_ForbiddenCatchAllTypeNames()
    {
        var result = Types.InAssembly(typeof(BaseEntity).Assembly)
            .ShouldNot()
            .HaveNameMatching(".*(Helper|Utils|Manager|Common).*")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"les types fourre-tout sont interdits par RULES.md : {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Codebase_ShouldNotResideIn_ForbiddenCatchAllNamespaces()
    {
        var result = Types.InAssembly(typeof(BaseEntity).Assembly)
            .ShouldNot()
            .ResideInNamespaceMatching(@".*\.(Helper|Helpers|Utils|Manager|Managers|Common)(\..*)?$")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"les namespaces fourre-tout sont interdits par RULES.md : {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Codebase_ShouldNotContain_WarningSuppressions()
    {
        var assembly = typeof(BaseEntity).Assembly;
        var suppressedMembers = assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(System.Diagnostics.CodeAnalysis.SuppressMessageAttribute), true)
                .Select(_ => t.FullName ?? t.Name)
                .Concat(t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes(typeof(System.Diagnostics.CodeAnalysis.SuppressMessageAttribute), false).Length > 0)
                    .Select(m => $"{t.Name}.{m.Name}")))
            .ToList();

        suppressedMembers.Should().BeEmpty("aucun attribut [SuppressMessage] n'est autorisé par RULES.md");

        var projectDir = FindProjectDir();
        var violations = new List<string>();
        foreach (var file in EnumerateSourceFiles(projectDir, "*.cs", "*.cshtml"))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (WarningSuppressionRegex().IsMatch(lines[i]))
                {
                    violations.Add($"{Path.GetRelativePath(projectDir, file)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        violations.Should().BeEmpty("les directives #pragma warning disable, // NOSONAR et [SuppressMessage] sont interdites par RULES.md");
    }

    [Fact]
    public void Codebase_ShouldNotContain_NullForgivingOperators()
    {
        var projectDir = FindProjectDir();
        var violations = new List<string>();

        foreach (var file in EnumerateSourceFiles(projectDir, "*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (NullForgivingRegex().IsMatch(line))
                {
                    violations.Add($"{Path.GetRelativePath(projectDir, file)}:{i + 1}: {line.Trim()}");
                }
            }
        }

        violations.Should().BeEmpty("l'opérateur null-forgiving (!) est interdit par RULES.md");
    }

    [Fact]
    public void RazorPages_ShouldNotUse_RedirectToPageWithAbsoluteRoute()
    {
        var projectDir = FindProjectDir();
        var violations = new List<string>();

        foreach (var file in EnumerateSourceFiles(projectDir, "*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (RedirectToPageWithRouteRegex().IsMatch(lines[i]))
                {
                    violations.Add($"{Path.GetRelativePath(projectDir, file)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        violations.Should().BeEmpty("avec les Vertical Slices (RootDirectory = '/'), utilisez LocalRedirect(\"~/Route\") au lieu de RedirectToPage(\"/Slice/...\")");
    }

    [Fact]
    public void ViewsAndStyles_ShouldBe100PercentOffline_WithoutExternalCdnLinks()
    {
        var projectDir = FindProjectDir();
        var violations = new List<string>();

        foreach (var file in EnumerateSourceFiles(projectDir, "*.cshtml", "*.css"))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (ExternalCdnLinkRegex().IsMatch(lines[i]))
                {
                    violations.Add($"{Path.GetRelativePath(projectDir, file)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        violations.Should().BeEmpty("L'application doit rester 100 % autonome hors-ligne : aucune dépendance CDN externe (http/https) n'est autorisée");
    }

    [Fact]
    public void Codebase_ShouldNotContain_ForbiddenAiBuzzwordTypeNames()
    {
        var result = Types.InAssembly(typeof(BaseEntity).Assembly)
            .ShouldNot()
            .HaveNameMatching(@".*(Robust|Resilient|Seamless|Smart|Comprehensive|Ultimate).*")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"les qualificatifs mélioratifs et marqueurs lexicaux d'IA sont interdits par RULES.md : {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void TestMethods_WhenInspected_MustFollowThreePartNamingConvention()
    {
        var solutionDir = FindSolutionDir();
        var testsDir = Path.Combine(solutionDir, "tests");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(testsDir, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(testsDir, file);
            if (relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            bool hasTestAttribute = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("[Fact", StringComparison.Ordinal) ||
                    line.StartsWith("[Theory", StringComparison.Ordinal))
                {
                    hasTestAttribute = true;
                    continue;
                }

                if (hasTestAttribute)
                {
                    var match = TestMethodDeclarationRegex().Match(line);
                    if (match.Success)
                    {
                        var testName = match.Groups["name"].Value;
                        hasTestAttribute = false;

                        var parts = testName.Split('_');
                        if (parts.Length != 3 ||
                            string.IsNullOrWhiteSpace(parts[0]) ||
                            string.IsNullOrWhiteSpace(parts[1]) ||
                            string.IsNullOrWhiteSpace(parts[2]) ||
                            testName.StartsWith("When", StringComparison.Ordinal) ||
                            testName.StartsWith("Should", StringComparison.Ordinal))
                        {
                            violations.Add($"{relative}:{i + 1} => {testName}");
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(line) && !IsAttributeOrCommentLine(line))
                    {
                        hasTestAttribute = false;
                    }
                }
            }
        }

        violations.Should().BeEmpty("tout test unitaire ou d'intégration doit respecter strictement le motif MethodeCible_ScenarioOuCondition_ResultatAttendu (Règle 7 d'AGENTS.md)");
    }

    private static bool IsAttributeOrCommentLine(string line) =>
        line.StartsWith('[') || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("/*", StringComparison.Ordinal);

    private static string FindSolutionDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var sln = Path.Combine(dir.FullName, "Ledger.slnx");
            if (File.Exists(sln))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Impossible de localiser le répertoire de la solution.");
    }

    [GeneratedRegex(@"public\s+(?:async\s+)?(?:Task|void|ValueTask)\s+(?<name>[A-Za-z0-9_]+)\s*\(", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TestMethodDeclarationRegex();

    [GeneratedRegex(@"#pragma\s+warning\s+disable|//\s*NOSONAR|\[\s*SuppressMessage", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex WarningSuppressionRegex();

    [GeneratedRegex(@"\bnull!\s*;|\w+!\.\w+", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex NullForgivingRegex();

    [GeneratedRegex(@"\bRedirectToPage\s*\(\s*""/", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RedirectToPageWithRouteRegex();

    [GeneratedRegex(@"<(?:script|link)\b[^>]*(?:src|href)\s*=\s*[""']https?://|@import\s+url\s*\(\s*[""']?https?://", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ExternalCdnLinkRegex();
}
