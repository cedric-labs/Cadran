using System.Text.RegularExpressions;
using Ledger.Features.RulesEngine.Entities;
using Microsoft.Extensions.Logging;

namespace Ledger.Features.RulesEngine.Services;

/// <summary>
/// Compile une seule fois le motif de chaque règle, avec un délai d'exécution borné contre les attaques par expression régulière.
/// </summary>
internal static class RuleRegexCompiler
{
    /// <summary>
    /// Associe chaque règle à son expression compilée ; un motif invalide est journalisé et la règle reçoit une expression nulle.
    /// </summary>
    public static List<(CategorizationRule Rule, Regex? CompiledRegex)> Compile(IEnumerable<CategorizationRule> rules, TimeSpan timeout, ILogger logger)
    {
        var compiled = new List<(CategorizationRule Rule, Regex? CompiledRegex)>();
        foreach (var rule in rules)
        {
            Regex? regex = null;
            try
            {
                regex = new Regex(rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, timeout);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Regex invalide pour la règle {RuleName} ({Pattern})", rule.Name, rule.Pattern);
            }

            compiled.Add((rule, regex));
        }

        return compiled;
    }
}
