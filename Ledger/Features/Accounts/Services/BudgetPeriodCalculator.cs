namespace Ledger.Features.Accounts.Services;

/// <summary>
/// Calculateur métier des périodes budgétaires et salariales prédéfinies pour le filtrage des opérations bancaires.
/// Délègue les calculs de plages temporelles au framework partagé <see cref="StatementForge.Analysis.BudgetPeriodCalculator"/>.
/// </summary>
public static class BudgetPeriodCalculator
{
    public const string CurrentMonth = StatementForge.Analysis.BudgetPeriodCalculator.CurrentMonth;
    public const string TwoWeeks = StatementForge.Analysis.BudgetPeriodCalculator.TwoWeeks;
    public const string LastMonth = StatementForge.Analysis.BudgetPeriodCalculator.LastMonth;
    public const string ThreeMonths = StatementForge.Analysis.BudgetPeriodCalculator.ThreeMonths;
    public const string SixMonths = StatementForge.Analysis.BudgetPeriodCalculator.SixMonths;
    public const string All = StatementForge.Analysis.BudgetPeriodCalculator.All;
    public const string Custom = StatementForge.Analysis.BudgetPeriodCalculator.Custom;

    /// <summary>
    /// Résout la période et les dates de début/fin en fonction des paramètres transmis.
    /// </summary>
    public static (DateTime? StartDate, DateTime? EndDate, string Period) ResolvePeriod(
        string? requestedPeriod,
        DateTime? requestedStartDate,
        DateTime? requestedEndDate,
        string? defaultPeriodFallback = CurrentMonth,
        int salaryMonthStartDay = 1,
        DateTime? today = null) =>
        StatementForge.Analysis.BudgetPeriodCalculator.ResolvePeriod(
            requestedPeriod,
            requestedStartDate,
            requestedEndDate,
            defaultPeriodFallback,
            salaryMonthStartDay,
            today);

    /// <summary>
    /// Calcule les bornes du mois courant (calendaire ou salarial glissant).
    /// </summary>
    public static (DateTime? StartDate, DateTime? EndDate, string Period) GetCurrentMonthRange(DateTime today, int salaryMonthStartDay = 1) =>
        StatementForge.Analysis.BudgetPeriodCalculator.GetCurrentMonthRange(today, salaryMonthStartDay);

    /// <summary>
    /// Calcule les bornes du mois précédent (calendaire ou salarial glissant).
    /// </summary>
    public static (DateTime? StartDate, DateTime? EndDate, string Period) GetLastMonthRange(DateTime today, int salaryMonthStartDay = 1) =>
        StatementForge.Analysis.BudgetPeriodCalculator.GetLastMonthRange(today, salaryMonthStartDay);
}
