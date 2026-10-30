using System.Text.RegularExpressions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Models;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Recurring.Services;

public sealed class RecurringExpenseService(AppDbContext dbContext) : IRecurringExpenseService
{
    public Task<List<RecurringExpense>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.RecurringExpenses
            .Include(r => r.TargetCategory)
            .Include(r => r.TargetBankAccount)
            .OrderBy(r => r.ExpectedDayOfMonth ?? 99)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<RecurringExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.RecurringExpenses
            .Include(r => r.TargetCategory)
            .Include(r => r.TargetBankAccount)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<RecurringExpense> CreateAsync(RecurringExpense expense, CancellationToken cancellationToken = default)
    {
        expense.UserId = string.Empty; // Injecté automatiquement par SaveChangesAsync
        dbContext.RecurringExpenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return expense;
    }

    public async Task<RecurringExpense?> UpdateAsync(RecurringExpense expense, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.RecurringExpenses.FirstOrDefaultAsync(r => r.Id == expense.Id, cancellationToken).ConfigureAwait(false);
        if (existing == null) return null;

        existing.Name = expense.Name.Trim();
        existing.Pattern = expense.Pattern.Trim();
        existing.ExpectedAmount = expense.ExpectedAmount;
        existing.AmountTolerancePercentage = expense.AmountTolerancePercentage;
        existing.Frequency = expense.Frequency;
        existing.ExpectedDayOfMonth = expense.ExpectedDayOfMonth;
        existing.TargetCategoryId = expense.TargetCategoryId;
        existing.TargetBankAccountId = expense.TargetBankAccountId;
        existing.IsActive = expense.IsActive;
        existing.Notes = expense.Notes;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.RecurringExpenses.FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        if (existing == null) return false;

        dbContext.RecurringExpenses.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<RecurringExpense> ConfirmCandidateAsync(RecurringCandidateDto candidate, CancellationToken cancellationToken = default)
    {
        var cleanPattern = candidate.Pattern.Trim();
        var cleanName = candidate.SuggestedName.Trim();

        // Vérifier si une charge avec le même motif ou le même nom existe déjà pour éviter tout doublon
        var existing = await dbContext.RecurringExpenses
            .FirstOrDefaultAsync(r => EF.Functions.Like(r.Pattern, cleanPattern) ||
                                      EF.Functions.Like(r.Name, cleanName), cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            // Déjà existant : mettre à jour les métadonnées de détection sans recréer de doublon
            existing.LastDetectedDate = candidate.LastDate;
            existing.LastDetectedAmount = candidate.LastAmount;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }

        var expense = new RecurringExpense
        {
            UserId = string.Empty,
            Name = cleanName,
            Pattern = cleanPattern,
            ExpectedAmount = candidate.LastAmount > 0 ? candidate.LastAmount : candidate.AverageAmount,
            AmountTolerancePercentage = 0.05m,
            Frequency = candidate.Frequency,
            ExpectedDayOfMonth = candidate.ExpectedDayOfMonth,
            TargetCategoryId = candidate.SuggestedCategoryId,
            TargetBankAccountId = candidate.BankAccountId,
            IsActive = true,
            AutoDetected = true,
            LastDetectedDate = candidate.LastDate,
            LastDetectedAmount = candidate.LastAmount
        };

        dbContext.RecurringExpenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return expense;
    }

    private const string IgnoredPatternsSettingKey = "Recurring:IgnoredPatterns";

    public async Task<List<string>> GetIgnoredPatternsAsync(CancellationToken cancellationToken = default)
    {
        var setting = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == IgnoredPatternsSettingKey, cancellationToken).ConfigureAwait(false);
        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return new List<string>();
        }

        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(setting.Value);
            return list ?? new List<string>();
        }
        catch (System.Text.Json.JsonException)
        {
            // Repli gracieux : si la liste des motifs ignorés est corrompue dans les paramètres, on renvoie une liste vide
            return new List<string>();
        }
    }

    public async Task IgnoreCandidateAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return;

        var cleanPattern = pattern.Trim();
        var currentList = await GetIgnoredPatternsAsync(cancellationToken).ConfigureAwait(false);

        if (!currentList.Any(p => string.Equals(p, cleanPattern, StringComparison.OrdinalIgnoreCase)))
        {
            currentList.Add(cleanPattern);

            var setting = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == IgnoredPatternsSettingKey, cancellationToken).ConfigureAwait(false);
            var jsonValue = System.Text.Json.JsonSerializer.Serialize(currentList);

            if (setting == null)
            {
                dbContext.AppSettings.Add(new Ledger.Infrastructure.Configuration.AppSettingEntity
                {
                    Key = IgnoredPatternsSettingKey,
                    Value = jsonValue,
                    Description = "Liste des motifs récurrents ignorés définitivement par l'utilisateur",
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                setting.Value = jsonValue;
                setting.UpdatedAtUtc = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task UnignoreCandidateAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return;

        var cleanPattern = pattern.Trim();
        var currentList = await GetIgnoredPatternsAsync(cancellationToken).ConfigureAwait(false);

        if (currentList.RemoveAll(p => string.Equals(p, cleanPattern, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            var setting = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == IgnoredPatternsSettingKey, cancellationToken).ConfigureAwait(false);
            if (setting != null)
            {
                setting.Value = System.Text.Json.JsonSerializer.Serialize(currentList);
                setting.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<MonthlyRecurringSummaryDto> GetMonthlySummaryAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var activeExpenses = await dbContext.RecurringExpenses
            .Include(r => r.TargetCategory)
            .Include(r => r.TargetBankAccount)
            .Where(r => r.IsActive)
            .OrderBy(r => r.ExpectedDayOfMonth ?? 99)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var startOfMonth = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

        // Transactions du mois sélectionné (prise en compte de la date budgétaire décalée)
        var monthTransactions = await dbContext.Transactions
            .Include(t => t.Category)
            .Where(t => (t.BudgetDate ?? t.Date) >= startOfMonth && (t.BudgetDate ?? t.Date) <= endOfMonth)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var today = DateTime.Today;
        var summary = new MonthlyRecurringSummaryDto
        {
            Year = year,
            Month = month
        };

        // Calcul des revenus du mois
        var creditTransactions = monthTransactions.Where(t => t.Type == TransactionType.Credit).ToList();
        summary.TotalIncome = creditTransactions.Sum(t => t.Amount);
        summary.SalaryIncome = creditTransactions
            .Where(t => t.Category != null && t.Category.IsSalary)
            .Sum(t => t.Amount);

        // Suivi de chaque charge récurrente
        var usedTxIds = new HashSet<Guid>();

        foreach (var expense in activeExpenses)
        {
            // Vérifier si cette charge s'applique ce mois-ci selon sa fréquence
            if (!AppliesToMonth(expense.Frequency, month))
            {
                continue;
            }

            // Recherche d'une transaction correspondante dans le mois
            var match = monthTransactions
                .Where(t => t.Type == TransactionType.Debit && !usedTxIds.Contains(t.Id))
                .Where(t => expense.TargetBankAccountId == null || t.BankAccountId == expense.TargetBankAccountId.Value)
                .Where(t => MatchesPattern(t.Label, t.RawLabel, expense.Pattern))
                .Where(t => expense.MatchesAmount(t.Amount))
                .OrderBy(t => Math.Abs(t.Amount - expense.ExpectedAmount))
                .FirstOrDefault();

            var item = new MonthlyRecurringItemDto
            {
                Expense = expense,
                ExpectedAmount = expense.ExpectedAmount
            };

            if (match != null)
            {
                usedTxIds.Add(match.Id);
                item.Status = RecurringMonthStatus.Debited;
                item.ActualDebitedAmount = match.Amount;
                item.ActualDebitedDate = match.BudgetDate ?? match.Date;
                item.ActualTransactionId = match.Id;
                item.ActualTransactionLabel = match.Label;

                summary.TotalDebited += match.Amount;
            }
            else
            {
                // Pas encore débité : en attente ou en retard
                var isCurrentMonth = (year == today.Year && month == today.Month);
                var isPastMonth = (startOfMonth < new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc));

                if (isPastMonth)
                {
                    item.Status = RecurringMonthStatus.Overdue;
                }
                else if (isCurrentMonth && expense.ExpectedDayOfMonth.HasValue && today.Day > expense.ExpectedDayOfMonth.Value + 3)
                {
                    item.Status = RecurringMonthStatus.Overdue;
                }
                else
                {
                    item.Status = RecurringMonthStatus.Pending;
                }

                summary.TotalPending += expense.ExpectedAmount;
            }

            summary.TotalExpected += expense.ExpectedAmount;
            summary.Items.Add(item);
        }

        return summary;
    }

    public async Task<List<RecurringCandidateDto>> DetectCandidatesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Charger les règles récurrentes déjà existantes et les motifs ignorés
        var existingExpenses = (await dbContext.RecurringExpenses
            .Select(r => new { r.Id, r.Name, r.Pattern })
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(r => new { r.Id, Name = r.Name.ToLowerInvariant(), Pattern = r.Pattern.ToLowerInvariant() })
            .ToList();

        var ignoredPatterns = await GetIgnoredPatternsAsync(cancellationToken).ConfigureAwait(false);

        // 2. Analyser les transactions de dépenses sur les 12 derniers mois
        var cutoffDate = DateTime.Today.AddMonths(-12);
        var transactions = await dbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.BankAccount)
            .Where(t => t.Type == TransactionType.Debit && t.Date >= cutoffDate)
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (transactions.Count < 3) return new List<RecurringCandidateDto>();

        // 3. Regrouper les transactions par "clé d'émetteur" nettoyée
        var rawGroups = new Dictionary<string, List<Transaction>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tx in transactions)
        {
            var cleanedKey = CleanTransactionLabel(tx.Label, tx.RawLabel);
            if (string.IsNullOrWhiteSpace(cleanedKey) || cleanedKey.Length < 3) continue;

            if (!rawGroups.TryGetValue(cleanedKey, out var list))
            {
                list = new List<Transaction>();
                rawGroups[cleanedKey] = list;
            }
            list.Add(tx);
        }

        // 3b. Sous-regroupement intelligent : si un émetteur (ex: AXA) a plusieurs contrats avec des montants stables distincts
        // (ex: un contrat à 45€ et un contrat à 25€), on les découpe en sous-groupes par palier de montant !
        var groups = new Dictionary<string, List<Transaction>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, txList) in rawGroups)
        {
            if (txList.Count < 2) continue;

            // Détecter si les montants sont dispersés en plusieurs montants réguliers distincts
            var distinctAmounts = txList
                .GroupBy(t => Math.Round(t.Amount, 1))
                .Where(g => g.Count() >= 2)
                .ToList();

            if (distinctAmounts.Count > 1)
            {
                // Multi-contrats sous le même émetteur ! Séparer par montant
                foreach (var amountGroup in distinctAmounts)
                {
                    var subList = amountGroup.OrderBy(t => t.Date).ToList();
                    var subKey = $"{key} ({amountGroup.Key:0.00} €)";
                    groups[subKey] = subList;
                }
            }
            else
            {
                groups[key] = txList;
            }
        }

        var candidates = new List<RecurringCandidateDto>();

        // 4. Analyser la cadence temporelle et la consistance pour chaque groupe
        foreach (var kvp in groups)
        {
            var rawKey = kvp.Key;
            var txList = kvp.Value.OrderBy(t => t.Date).ToList();

            // Il faut au moins 2 ou 3 occurrences
            if (txList.Count < 2) continue;

            // Si rawKey comporte un suffixe de montant (ex: "AXA (45.00 €)"), en extraire le pattern de recherche
            var matchingPattern = rawKey.Contains('(', StringComparison.Ordinal) ? rawKey.Substring(0, rawKey.IndexOf('(', StringComparison.Ordinal)).Trim() : rawKey.Trim();
            var cleanPatternLower = matchingPattern.ToLowerInvariant();
            var suggestedName = FormatSuggestedName(rawKey);
            var cleanSuggestedNameLower = suggestedName.Trim().ToLowerInvariant();

            // Vérifier si ce motif a été ignoré définitivement par l'utilisateur
            if (ignoredPatterns.Any(p =>
                string.Equals(p, matchingPattern, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p, rawKey, StringComparison.OrdinalIgnoreCase) ||
                matchingPattern.Contains(p, StringComparison.OrdinalIgnoreCase) ||
                p.Contains(matchingPattern, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Vérifier si cette charge est déjà couverte par une règle existante
            var matchingExisting = existingExpenses.FirstOrDefault(e =>
                string.Equals(e.Pattern, cleanPatternLower, StringComparison.Ordinal) ||
                cleanPatternLower.Contains(e.Pattern, StringComparison.Ordinal) ||
                e.Pattern.Contains(cleanPatternLower, StringComparison.Ordinal) ||
                string.Equals(e.Name, cleanSuggestedNameLower, StringComparison.Ordinal) ||
                cleanSuggestedNameLower.Contains(e.Name, StringComparison.Ordinal) ||
                e.Name.Contains(cleanSuggestedNameLower, StringComparison.Ordinal));

            // Calcul des écarts de jours entre occurrences consécutives
            var intervals = new List<int>();
            for (int i = 1; i < txList.Count; i++)
            {
                var days = (txList[i].Date - txList[i - 1].Date).Days;
                if (days > 0) intervals.Add(days);
            }

            if (intervals.Count == 0) continue;

            var avgInterval = intervals.Average();
            RecurringFrequency detectedFrequency;
            bool isRegularCadence = false;

            // Détection de la fréquence
            if (avgInterval >= 25 && avgInterval <= 36)
            {
                detectedFrequency = RecurringFrequency.Monthly;
                // Vérifier que chaque intervalle est proche d'un mois (tolérance ±12 jours)
                isRegularCadence = intervals.All(d => d >= 20 && d <= 45);
            }
            else if (avgInterval >= 80 && avgInterval <= 100)
            {
                detectedFrequency = RecurringFrequency.Quarterly;
                isRegularCadence = intervals.All(d => d >= 75 && d <= 110);
            }
            else if (avgInterval >= 340 && avgInterval <= 390)
            {
                detectedFrequency = RecurringFrequency.Annual;
                isRegularCadence = true;
            }
            else
            {
                // Intervalle irrégulier
                continue;
            }

            if (!isRegularCadence && txList.Count < 3) continue;

            // Analyse des montants
            var amounts = txList.Select(t => t.Amount).ToList();
            var avgAmount = Math.Round(amounts.Average(), 2);
            var lastAmount = amounts[^1];
            var minAmount = amounts.Min();
            var maxAmount = amounts.Max();

            // Vérifier la stabilité du montant (le max ne doit pas être 3x le min pour un abonnement)
            if (minAmount > 0 && (maxAmount / minAmount) > 2.5m)
            {
                continue;
            }

            // Jour du mois habituel
            var daysOfMonth = txList.Select(t => t.Date.Day).ToList();
            var medianDay = daysOfMonth.Order().ElementAt(daysOfMonth.Count / 2);

            // Catégorie la plus fréquente
            var categoryGroup = txList
                .Select(t => t.Category)
                .OfType<Ledger.Features.Categories.Entities.Category>()
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            var accountGroup = txList
                .Select(t => t.BankAccount)
                .OfType<Ledger.Features.Accounts.Entities.BankAccount>()
                .GroupBy(b => b)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            // Score de confiance (50 à 95%)
            int score = 50;
            if (txList.Count >= 3) score += 20;
            if (txList.Count >= 5) score += 15;
            if (Math.Abs(maxAmount - minAmount) < 0.10m) score += 15; // Montant strictement fixe

            var candidate = new RecurringCandidateDto
            {
                Pattern = matchingPattern,
                SuggestedName = suggestedName,
                AverageAmount = avgAmount,
                LastAmount = lastAmount,
                ExpectedDayOfMonth = medianDay,
                Frequency = detectedFrequency,
                SuggestedCategoryId = categoryGroup?.Key.Id,
                SuggestedCategoryName = categoryGroup?.Key.Name,
                SuggestedCategoryIcon = categoryGroup?.Key.IconName,
                SuggestedCategoryColor = categoryGroup?.Key.ColorHex,
                BankAccountId = accountGroup?.Key.Id,
                BankAccountName = accountGroup?.Key.Name,
                OccurrencesCount = txList.Count,
                FirstDate = txList[0].Date,
                LastDate = txList[^1].Date,
                ConfidenceScore = Math.Min(98, score),
                IsAlreadyConfigured = matchingExisting != null,
                ConfiguredExpenseName = matchingExisting?.Name,
                SampleTransactions = txList.TakeLast(3).Select(t => new TransactionSampleDto
                {
                    Id = t.Id,
                    Date = t.Date,
                    Label = t.Label,
                    Amount = t.Amount
                }).ToList()
            };

            candidates.Add(candidate);
        }

        return candidates
            .OrderByDescending(c => c.ConfidenceScore)
            .ThenByDescending(c => c.OccurrencesCount)
            .ToList();
    }

    private static bool AppliesToMonth(RecurringFrequency freq, int month)
    {
        var mapped = freq switch
        {
            RecurringFrequency.Monthly => StatementForge.Analysis.StatementRecurrenceFrequency.Monthly,
            RecurringFrequency.Quarterly => StatementForge.Analysis.StatementRecurrenceFrequency.Quarterly,
            RecurringFrequency.Biannual => StatementForge.Analysis.StatementRecurrenceFrequency.Biannual,
            RecurringFrequency.Annual => StatementForge.Analysis.StatementRecurrenceFrequency.Annual,
            _ => StatementForge.Analysis.StatementRecurrenceFrequency.Monthly
        };
        return StatementForge.Analysis.RecurringCadenceAnalyzer.AppliesToMonth(mapped, month);
    }

    public static bool MatchesAmount(decimal expectedAmount, decimal tolerancePercentage, decimal transactionAmount) =>
        RecurringExpense.MatchesAmount(expectedAmount, tolerancePercentage, transactionAmount);

    public static bool MatchesPattern(string label, string? rawLabel, string pattern) =>
        StatementForge.Analysis.StatementLabelAnalyzer.MatchesPattern(label, rawLabel, pattern);

    /// <summary>
    /// Nettoie les préfixes bancaires bruyants (PRLV, VIR, CB, dates, etc.) pour extraire le cœur du marchand
    /// et son descripteur significatif via <see cref="StatementForge.Analysis.StatementLabelAnalyzer"/>.
    /// </summary>
    public static string CleanTransactionLabel(string label, string? rawLabel) =>
        StatementForge.Analysis.StatementLabelAnalyzer.CleanTransactionLabel(label, rawLabel);

    public static string FormatSuggestedName(string pattern) =>
        StatementForge.Analysis.StatementLabelAnalyzer.FormatSuggestedName(pattern);

    public async Task<Dictionary<Guid, RecurringMatchDto>> GetMatchesForTransactionsAsync(
        IEnumerable<Transaction> transactions,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, RecurringMatchDto>();
        var txList = transactions?.ToList();
        if (txList == null || txList.Count == 0)
        {
            return result;
        }

        var activeExpenses = await dbContext.RecurringExpenses
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (activeExpenses.Count == 0)
        {
            return txList.ToDictionary(
                tx => tx.Id,
                tx => new RecurringMatchDto(tx.Id, 0, null, new List<string>(), null, new List<Guid>()));
        }

        foreach (var tx in txList)
        {
            var matchingExpenses = new List<RecurringExpense>();

            foreach (var exp in activeExpenses)
            {
                if (exp.TargetBankAccountId.HasValue && tx.BankAccountId != Guid.Empty && exp.TargetBankAccountId.Value != tx.BankAccountId)
                {
                    continue;
                }

                if (MatchesPattern(tx.Label, tx.RawLabel, exp.Pattern) && exp.MatchesAmount(tx.Amount))
                {
                    matchingExpenses.Add(exp);
                }
            }

            var firstExpense = matchingExpenses
                .OrderBy(e => Math.Abs(tx.Amount - e.ExpectedAmount))
                .FirstOrDefault();
            result[tx.Id] = new RecurringMatchDto(
                TransactionId: tx.Id,
                MatchCount: matchingExpenses.Count,
                FirstExpenseName: firstExpense?.Name,
                MatchingExpenseNames: matchingExpenses.Select(e => e.Name).ToList(),
                FirstExpenseId: firstExpense?.Id,
                MatchingExpenseIds: matchingExpenses.Select(e => e.Id).ToList()
            );
        }

        return result;
    }
}
