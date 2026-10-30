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
.ConfigureAwait(false) ?? throw new KeyNotFoundException($"Compte bancaire {accountId} non trouvé.");

        var balances = AccountBalances.Of(account);

        string? customTypeName = account.CustomAccountType?.Name;
        if (account.CustomAccountType != null)
        {
            customTypeName = await localizedEntityService.GetTranslationAsync(
                "CustomAccountType",
                account.CustomAccountType.Id,
                "Name",
                account.CustomAccountType.Name,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new AccountBalanceSummaryDto(
            account.Id,
            account.Name,
            account.Bank?.Name ?? "Banque",
            account.Type,
            balances.Current,
            balances.Cleared,
            balances.Pending,
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
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Tri en mémoire : SQLite ne sait pas ordonner sans tenir compte de la casse selon la culture, et sans tri explicite l'ordre dépend des identifiants Guid aléatoires.
        accounts.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

        var summaries = new List<AccountBalanceSummaryDto>();

        var totals = new NetWorthTotals();

        var typeIds = accounts
            .Select(a => a.CustomAccountTypeId)
            .OfType<Guid>()
            .Distinct();

        var typeTranslations = await localizedEntityService.GetTranslationsAsync(
            "CustomAccountType",
            typeIds,
            "Name",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var account in accounts)
        {
            var balances = AccountBalances.Of(account);

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
                balances.Current,
                balances.Cleared,
                balances.Pending,
                account.Currency,
                account.CustomAccountTypeId,
                localizedTypeName,
                account.CustomAccountType?.ColorHex,
                account.CustomAccountType?.IconName,
                account.CustomAccountType?.CategoryGroup);

            summaries.Add(summary);

            totals.Add(account, balances.Current);
        }

        return new DashboardSummaryDto(totals.NetWorth, totals.Liquid, totals.Savings, totals.Debts, summaries);
    }

    public async Task<decimal> CalculateBalanceAtDateAsync(Guid accountId, DateTime targetDate, CancellationToken cancellationToken = default)
    {
        var account = await dbContext.BankAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
.ConfigureAwait(false) ?? throw new KeyNotFoundException($"Compte bancaire {accountId} non trouvé.");

        var txsUntilDate = account.Transactions.Where(t => t.Date.Date <= targetDate.Date).ToList();
        decimal credits = txsUntilDate.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
        decimal debits = txsUntilDate.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);

        return account.InitialBalance + credits - debits;
    }
}
