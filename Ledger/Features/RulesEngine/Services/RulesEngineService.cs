using System.Text.RegularExpressions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.RulesEngine.Entities;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ledger.Features.RulesEngine.Services;

/// <summary>
/// Moteur de règles de catégorisation automatique.
/// Analyse les libellés bancaires via expressions régulières (Regex) compilées et sécurisées contre le ReDoS.
/// </summary>
public sealed class RulesEngineService(
    AppDbContext dbContext,
    IConfiguration configuration,
    ILogger<RulesEngineService> logger) : IRulesEngineService
{

    public async Task<RuleMatchResult> EvaluateAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        // Chargement des règles actives de l'utilisateur ordonnées par priorité
        var rules = await dbContext.CategorizationRules
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);

        var timeoutMs = configuration.GetValue("Rules:MaxExecutionTimeMs", 500);
        var regexTimeout = TimeSpan.FromMilliseconds(timeoutMs);

        var textToTest = $"{transaction.Label} {transaction.RawLabel ?? string.Empty}".Trim();

        foreach (var rule in rules)
        {
            // Vérification du filtre de compte bancaire
            if (rule.TargetBankAccountId.HasValue && rule.TargetBankAccountId.Value != transaction.BankAccountId)
            {
                continue;
            }

            // Vérification des filtres de montants optionnels
            if (rule.MinAmount.HasValue && transaction.Amount < rule.MinAmount.Value)
            {
                continue;
            }

            if (rule.MaxAmount.HasValue && transaction.Amount > rule.MaxAmount.Value)
            {
                continue;
            }

            try
            {
                // Évaluation Regex protégée contre les attaques de déni de service (ReDoS)
                var isMatch = Regex.IsMatch(textToTest, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, regexTimeout);
                if (isMatch)
                {
                    return new RuleMatchResult(true, rule.TargetCategoryId, rule.Name, rule.ShiftToNextMonth, rule.TargetMerchantId);
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                logger.LogWarning(ex, "Timeout lors de l'exécution de la règle {RuleName} (Pattern: {Pattern}) sur l'opération {Label}",
                    rule.Name, rule.Pattern, transaction.Label);
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "Pattern Regex syntaxiquement invalide pour la règle {RuleName}: {Pattern}",
                    rule.Name, rule.Pattern);
            }
        }

        return new RuleMatchResult(false, null, null, false, null);
    }

    public async Task<Dictionary<Guid, DetailedRuleMatch>> GetMatchesForTransactionsAsync(
        IEnumerable<Transaction> transactions,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, DetailedRuleMatch>();
        var txList = transactions?.ToList();
        if (txList == null || txList.Count == 0)
        {
            return result;
        }

        var rules = await dbContext.CategorizationRules
            .Include(r => r.TargetCategory)
            .Include(r => r.TargetMerchant)
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return txList.ToDictionary(
                tx => tx.Id,
                tx => new DetailedRuleMatch(tx.Id, 0, null, null, null, new List<string>(), null, null));
        }

        var timeoutMs = configuration.GetValue("Rules:MaxExecutionTimeMs", 500);
        var regexTimeout = TimeSpan.FromMilliseconds(timeoutMs);

        // Pré-compilation / préparation des Regex pour éviter de re-parser par transaction
        var compiledRules = new List<(CategorizationRule Rule, Regex? CompiledRegex)>();
        foreach (var rule in rules)
        {
            Regex? reg = null;
            try
            {
                reg = new Regex(rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, regexTimeout);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Regex invalide pour la règle {RuleName} ({Pattern})", rule.Name, rule.Pattern);
            }
            compiledRules.Add((rule, reg));
        }

        foreach (var tx in txList)
        {
            var textToTest = $"{tx.Label} {tx.RawLabel ?? string.Empty}".Trim();
            var matchingRules = new List<CategorizationRule>();

            foreach (var (rule, reg) in compiledRules)
            {
                if (reg == null) continue;

                if (rule.TargetBankAccountId.HasValue && rule.TargetBankAccountId.Value != tx.BankAccountId)
                    continue;

                if (rule.MinAmount.HasValue && tx.Amount < rule.MinAmount.Value)
                    continue;

                if (rule.MaxAmount.HasValue && tx.Amount > rule.MaxAmount.Value)
                    continue;

                try
                {
                    if (reg.IsMatch(textToTest))
                    {
                        matchingRules.Add(rule);
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Une règle utilisateur trop complexe dépassant le temps d'exécution imparti est ignorée pour cette transaction afin de ne pas bloquer l'évaluation
                }
            }

            var firstRule = matchingRules.FirstOrDefault();
            result[tx.Id] = new DetailedRuleMatch(
                TransactionId: tx.Id,
                MatchCount: matchingRules.Count,
                FirstRuleName: firstRule?.Name,
                FirstTargetCategoryId: firstRule?.TargetCategoryId,
                FirstCategoryName: firstRule?.TargetCategory?.Name,
                MatchingRuleNames: matchingRules.Select(r => r.Name).ToList(),
                FirstTargetMerchantId: firstRule?.TargetMerchantId,
                FirstMerchantName: firstRule?.TargetMerchant?.Name);
        }

        return result;
    }

    public Task<int> ApplyRulesToTransactionsAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        return ApplyRulesToTransactionsAsync(transactions, overwriteExisting: false, singleRuleId: null, cancellationToken);
    }

    public async Task<int> ApplyRulesToTransactionsAsync(
        IEnumerable<Transaction> transactions,
        bool overwriteExisting,
        Guid? singleRuleId = null,
        CancellationToken cancellationToken = default)
    {
        int matchedCount = 0;
        var rulesQuery = dbContext.CategorizationRules
            .Where(r => r.IsActive);

        if (singleRuleId.HasValue)
        {
            rulesQuery = rulesQuery.Where(r => r.Id == singleRuleId.Value);
        }

        var rules = await rulesQuery
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return 0;
        }

        var timeoutMs = configuration.GetValue("Rules:MaxExecutionTimeMs", 500);
        var regexTimeout = TimeSpan.FromMilliseconds(timeoutMs);

        foreach (var tx in transactions)
        {
            if (!overwriteExisting && tx.CategoryId.HasValue)
            {
                continue; // Déjà catégorisé et forçage désactivé
            }

            var textToTest = $"{tx.Label} {tx.RawLabel ?? string.Empty}".Trim();

            foreach (var rule in rules)
            {
                if (rule.TargetBankAccountId.HasValue && rule.TargetBankAccountId.Value != tx.BankAccountId)
                    continue;

                if (rule.MinAmount.HasValue && tx.Amount < rule.MinAmount.Value)
                    continue;

                if (rule.MaxAmount.HasValue && tx.Amount > rule.MaxAmount.Value)
                    continue;

                try
                {
                    if (Regex.IsMatch(textToTest, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, regexTimeout))
                    {
                        bool changed = false;
                        if (tx.CategoryId != rule.TargetCategoryId)
                        {
                            tx.CategoryId = rule.TargetCategoryId;
                            changed = true;
                        }

                        if (rule.TargetMerchantId.HasValue && (overwriteExisting || !tx.MerchantId.HasValue) && tx.MerchantId != rule.TargetMerchantId.Value)
                        {
                            tx.MerchantId = rule.TargetMerchantId.Value;
                            changed = true;
                        }

                        if (rule.ShiftToNextMonth)
                        {
                            var targetBudgetDate = new DateTime(tx.Date.Year, tx.Date.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                            if (tx.BudgetDate != targetBudgetDate)
                            {
                                tx.BudgetDate = targetBudgetDate;
                                changed = true;
                            }
                        }

                        if (changed)
                        {
                            matchedCount++;
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Erreur d'évaluation pour la règle {RuleId}", rule.Id);
                }
            }
        }

        return matchedCount;
    }

    public async Task<int> ApplyRulesToDatabaseAsync(
        bool overwriteExisting = false,
        Guid? bankAccountId = null,
        Guid? singleRuleId = null,
        CancellationToken cancellationToken = default)
    {
        var txQuery = dbContext.Transactions.AsQueryable();

        if (bankAccountId.HasValue)
        {
            txQuery = txQuery.Where(t => t.BankAccountId == bankAccountId.Value);
        }

        if (!overwriteExisting)
        {
            txQuery = txQuery.Where(t => t.CategoryId == null);
        }

        var transactions = await txQuery.ToListAsync(cancellationToken);
        if (transactions.Count == 0)
        {
            return 0;
        }

        var matchedCount = await ApplyRulesToTransactionsAsync(transactions, overwriteExisting, singleRuleId, cancellationToken);

        if (matchedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return matchedCount;
    }
}
