using System.Text.RegularExpressions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Models;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Recurring.Services;

public sealed class RecurringExpenseService(AppDbContext dbContext, TimeProvider timeProvider) : IRecurringExpenseService
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
                    UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
                });
            }
            else
            {
                setting.Value = jsonValue;
                setting.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
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
                setting.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
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

        return MonthlyRecurringSummaryBuilder.Build(year, month, timeProvider.GetLocalNow().Date, activeExpenses, monthTransactions);
    }

    public async Task<List<RecurringCandidateDto>> DetectCandidatesAsync(CancellationToken cancellationToken = default)
    {
        var existingRules = (await dbContext.RecurringExpenses
            .Select(r => new { r.Id, r.Name, r.Pattern })
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(r => new ExistingRecurringRule(r.Id, r.Name.ToLowerInvariant(), r.Pattern.ToLowerInvariant()))
            .ToList();

        var ignoredPatterns = await GetIgnoredPatternsAsync(cancellationToken).ConfigureAwait(false);

        var cutoffDate = timeProvider.GetLocalNow().Date.AddMonths(-12);
        var debits = await dbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.BankAccount)
            .Where(t => t.Type == TransactionType.Debit && t.Date >= cutoffDate)
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (debits.Count < 3)
        {
            return [];
        }

        return new RecurringCandidateDetector(existingRules, ignoredPatterns).Detect(debits);
    }

    internal static bool AppliesToMonth(RecurringFrequency freq, int month)
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
