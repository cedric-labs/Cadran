using Ledger.Features.Accounts.Entities;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Service de calcul des soldes bancaires et d'agrégation financière.
/// Bénéficie nativement du filtre global multi-utilisateur appliqué sur AppDbContext.
/// </summary>
public sealed class AccountBalanceService(
    AppDbContext dbContext,
    Ledger.Infrastructure.Configuration.ILocalizedEntityService localizedEntityService) : IAccountBalanceService
{
    public async Task<AccountBalanceSummaryDto> GetAccountBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await dbContext.BankAccounts
            .Include(a => a.Bank)
            .Include(a => a.CustomAccountType)
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Compte bancaire {accountId} non trouvé.");

        decimal initial = account.InitialBalance;
        decimal currentCredit = account.Transactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
        decimal currentDebit = account.Transactions.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);
        decimal currentBalance = initial + currentCredit - currentDebit;

        decimal clearedCredit = account.Transactions
            .Where(t => t.Type == TransactionType.Credit && t.Status != TransactionStatus.Pending)
            .Sum(t => t.Amount);
        decimal clearedDebit = account.Transactions
            .Where(t => t.Type == TransactionType.Debit && t.Status != TransactionStatus.Pending)
            .Sum(t => t.Amount);
        decimal clearedBalance = initial + clearedCredit - clearedDebit;

        decimal pendingBalance = currentBalance - clearedBalance;

        string? customTypeName = account.CustomAccountType?.Name;
        if (account.CustomAccountType != null)
        {
            customTypeName = await localizedEntityService.GetTranslationAsync(
                "CustomAccountType",
                account.CustomAccountType.Id,
                "Name",
                account.CustomAccountType.Name,
                cancellationToken: cancellationToken);
        }

        return new AccountBalanceSummaryDto(
            account.Id,
            account.Name,
            account.Bank?.Name ?? "Banque",
            account.Type,
            currentBalance,
            clearedBalance,
            pendingBalance,
            account.Currency,
            account.CustomAccountTypeId,
            customTypeName,
            account.CustomAccountType?.ColorHex,
            account.CustomAccountType?.IconName,
            account.CustomAccountType?.CategoryGroup);
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await dbContext.BankAccounts
            .Include(a => a.Bank)
            .Include(a => a.CustomAccountType)
            .Include(a => a.Transactions)
            .Where(a => !a.IsArchived)
            .ToListAsync(cancellationToken);

        var summaries = new List<AccountBalanceSummaryDto>();

        decimal totalLiquid = 0m;
        decimal totalSavings = 0m;
        decimal totalDebts = 0m;

        var typeIds = accounts
            .Select(a => a.CustomAccountTypeId)
            .OfType<Guid>()
            .Distinct();

        var typeTranslations = await localizedEntityService.GetTranslationsAsync(
            "CustomAccountType",
            typeIds,
            "Name",
            cancellationToken: cancellationToken);

        foreach (var account in accounts)
        {
            decimal credits = account.Transactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
            decimal debits = account.Transactions.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);
            decimal balance = account.InitialBalance + credits - debits;

            decimal clearedCredits = account.Transactions.Where(t => t.Type == TransactionType.Credit && t.Status != TransactionStatus.Pending).Sum(t => t.Amount);
            decimal clearedDebits = account.Transactions.Where(t => t.Type == TransactionType.Debit && t.Status != TransactionStatus.Pending).Sum(t => t.Amount);
            decimal clearedBalance = account.InitialBalance + clearedCredits - clearedDebits;

            string? localizedTypeName = account.CustomAccountType?.Name;
            if (account.CustomAccountTypeId != null && typeTranslations.TryGetValue(account.CustomAccountTypeId.Value, out var trans) && !string.IsNullOrWhiteSpace(trans))
            {
                localizedTypeName = trans;
            }

            var summary = new AccountBalanceSummaryDto(
                account.Id,
                account.Name,
                account.Bank?.Name ?? "Banque",
                account.Type,
                balance,
                clearedBalance,
                balance - clearedBalance,
                account.Currency,
                account.CustomAccountTypeId,
                localizedTypeName,
                account.CustomAccountType?.ColorHex,
                account.CustomAccountType?.IconName,
                account.CustomAccountType?.CategoryGroup);

            summaries.Add(summary);

            // Classification selon le type configurable en base de données si défini, sinon fallback sur l'enum
            AccountCategoryGroup effectiveGroup;
            if (account.CustomAccountType != null)
            {
                effectiveGroup = account.CustomAccountType.CategoryGroup;
            }
            else if (account.Type.IsLiquid())
            {
                effectiveGroup = AccountCategoryGroup.Liquid;
            }
            else if (account.Type == AccountType.CreditCard)
            {
                effectiveGroup = AccountCategoryGroup.Liquid;
            }
            else
            {
                effectiveGroup = AccountCategoryGroup.Savings;
            }

            if (effectiveGroup == AccountCategoryGroup.Liquid)
            {
                if (account.Type == AccountType.CreditCard && balance < 0)
                {
                    totalDebts += Math.Abs(balance);
                }
                else if (balance >= 0)
                {
                    totalLiquid += balance;
                }
                else
                {
                    totalDebts += Math.Abs(balance);
                }
            }
            else if (effectiveGroup == AccountCategoryGroup.Debt)
            {
                if (balance < 0) totalDebts += Math.Abs(balance);
                else totalLiquid += balance;
            }
            else
            {
                // Savings ou Investment
                totalSavings += balance;
            }
        }

        decimal netWorth = totalLiquid + totalSavings - totalDebts;

        return new DashboardSummaryDto(netWorth, totalLiquid, totalSavings, totalDebts, summaries);
    }

    public async Task<decimal> CalculateBalanceAtDateAsync(Guid accountId, DateTime targetDate, CancellationToken cancellationToken = default)
    {
        var account = await dbContext.BankAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Compte bancaire {accountId} non trouvé.");

        var txsUntilDate = account.Transactions.Where(t => t.Date.Date <= targetDate.Date).ToList();
        decimal credits = txsUntilDate.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
        decimal debits = txsUntilDate.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);

        return account.InitialBalance + credits - debits;
    }
}
