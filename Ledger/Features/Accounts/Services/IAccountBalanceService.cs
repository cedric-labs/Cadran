using Ledger.Features.Accounts.Entities;

namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Synthèse financière et soldes calculés pour un compte bancaire.
/// </summary>
public record AccountBalanceSummaryDto(
    Guid AccountId,
    string AccountName,
    string BankName,
    AccountType Type,
    decimal CurrentBalance,
    decimal ClearedBalance,
    decimal PendingBalance,
    string Currency,
    Guid? CustomAccountTypeId = null,
    string? CustomAccountTypeName = null,
    string? CustomAccountTypeColor = null,
    string? CustomAccountTypeIcon = null,
    AccountCategoryGroup? CustomCategoryGroup = null);

/// <summary>
/// Synthèse financière globale du patrimoine de l'utilisateur.
/// </summary>
public record DashboardSummaryDto(
    decimal TotalNetWorth,
    decimal TotalLiquidAssets,
    decimal TotalSavings,
    decimal TotalDebts,
    IReadOnlyList<AccountBalanceSummaryDto> Accounts);

/// <summary>
/// Contrat du service de calcul des soldes et synthèses de comptes bancaires.
/// </summary>
public interface IAccountBalanceService
{
    /// <summary>
    /// Calcule la synthèse et les soldes d'un compte bancaire identifié.
    /// </summary>
    /// <param name="accountId">Identifiant unique du compte bancaire.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Synthèse détaillée des soldes du compte.</returns>
    Task<AccountBalanceSummaryDto> GetAccountBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcule la synthèse globale de l'ensemble des comptes de l'utilisateur connecté.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Synthèse globale du patrimoine et liste des soldes par compte.</returns>
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcule le solde d'un compte bancaire à une date passée ou cible donnée.
    /// </summary>
    /// <param name="accountId">Identifiant unique du compte bancaire.</param>
    /// <param name="targetDate">Date cible jusqu'à laquelle les transactions sont prises en compte.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'opération asynchrone.</param>
    /// <returns>Montant du solde calculé à la date cible.</returns>
    Task<decimal> CalculateBalanceAtDateAsync(Guid accountId, DateTime targetDate, CancellationToken cancellationToken = default);
}
