using System.Globalization;
using System.Text.Json;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Recurring.Services;
using Ledger.Features.Reports.Entities;
using Ledger.Features.Reports.Models;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Ledger.Features.Reports.Services;

public sealed class ReportService(
    AppDbContext dbContext,
    ILocalizedEntityService localizedEntityService,
    IRecurringExpenseService recurringService,
    IStringLocalizer localizer) : IReportService
{
    private static readonly string[] DefaultPalette = new[]
    {
        "#3b82f6", "#10b981", "#f59e0b", "#ef4444", "#8b5cf6",
        "#ec4899", "#06b6d4", "#f97316", "#14b8a6", "#6366f1",
        "#84cc16", "#a855f7", "#0ea5e9", "#eab308", "#64748b"
    };

    public async Task<List<ReportDashboard>> GetDashboardsAsync(CancellationToken cancellationToken = default)
    {
        var dashboards = await dbContext.ReportDashboards
            .Include(d => d.Widgets.OrderBy(w => w.DisplayOrder))
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (dashboards.Count == 0)
        {
            await SeedDefaultDashboardAsync(cancellationToken).ConfigureAwait(false);
            dashboards = await dbContext.ReportDashboards
                .Include(d => d.Widgets.OrderBy(w => w.DisplayOrder))
                .OrderBy(d => d.DisplayOrder)
                .ThenBy(d => d.Name)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        return dashboards;
    }

    public Task<ReportDashboard?> GetDashboardByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.ReportDashboards
            .Include(d => d.Widgets.OrderBy(w => w.DisplayOrder))
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<ReportDashboard> CreateDashboardAsync(string name, string? description = null, CancellationToken cancellationToken = default)
    {
        var dashboard = new ReportDashboard
        {
            UserId = string.Empty,
            Name = name.Trim(),
            Description = description?.Trim(),
            IsDefault = false,
            DisplayOrder = 10
        };

        dbContext.ReportDashboards.Add(dashboard);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return dashboard;
    }

    public async Task<bool> DeleteDashboardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dashboard = await dbContext.ReportDashboards
            .Include(d => d.Widgets)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken).ConfigureAwait(false);

        if (dashboard == null) return false;

        dbContext.ReportWidgets.RemoveRange(dashboard.Widgets);
        dbContext.ReportDashboards.Remove(dashboard);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<ReportWidget> AddWidgetAsync(Guid dashboardId, string title, string widgetType, int widthCols, string? configJson = null, CancellationToken cancellationToken = default)
    {
        var count = await dbContext.ReportWidgets.CountAsync(w => w.DashboardId == dashboardId, cancellationToken).ConfigureAwait(false);

        var widget = new ReportWidget
        {
            DashboardId = dashboardId,
            Title = title.Trim(),
            WidgetType = widgetType.Trim(),
            WidthCols = widthCols > 0 ? widthCols : 6,
            DisplayOrder = count + 1,
            ConfigJson = !string.IsNullOrWhiteSpace(configJson) ? configJson : "{}"
        };

        dbContext.ReportWidgets.Add(widget);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return widget;
    }

    public async Task<ReportWidget?> UpdateWidgetAsync(Guid widgetId, string title, int widthCols, string? configJson = null, CancellationToken cancellationToken = default)
    {
        var widget = await dbContext.ReportWidgets.FirstOrDefaultAsync(w => w.Id == widgetId, cancellationToken).ConfigureAwait(false);
        if (widget == null) return null;

        widget.Title = title.Trim();
        widget.WidthCols = widthCols;
        if (configJson != null) widget.ConfigJson = configJson;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return widget;
    }

    public async Task<bool> DeleteWidgetAsync(Guid widgetId, CancellationToken cancellationToken = default)
    {
        var widget = await dbContext.ReportWidgets.FirstOrDefaultAsync(w => w.Id == widgetId, cancellationToken).ConfigureAwait(false);
        if (widget == null) return false;

        dbContext.ReportWidgets.Remove(widget);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task ResetDefaultDashboardAsync(CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.ReportDashboards
            .Include(d => d.Widgets)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (existing.Count > 0)
        {
            foreach (var d in existing)
            {
                dbContext.ReportWidgets.RemoveRange(d.Widgets);
            }
            dbContext.ReportDashboards.RemoveRange(existing);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await SeedDefaultDashboardAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<WidgetDataDto>> ComputeDashboardDataAsync(Guid dashboardId, CancellationToken cancellationToken = default)
    {
        var dashboard = await GetDashboardByIdAsync(dashboardId, cancellationToken).ConfigureAwait(false);
        if (dashboard == null) return new List<WidgetDataDto>();

        var list = new List<WidgetDataDto>();
        foreach (var widget in dashboard.Widgets.OrderBy(w => w.DisplayOrder))
        {
            var data = await ComputeWidgetDataAsync(widget, cancellationToken).ConfigureAwait(false);
            list.Add(data);
        }
        return list;
    }

    public async Task<WidgetDataDto> ComputeWidgetDataAsync(ReportWidget widget, CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(widget.ConfigJson);
        var (startDate, endDate, periodLabel) = ResolveDateRange(config.Period);

        var result = new WidgetDataDto
        {
            WidgetId = widget.Id,
            Title = widget.Title,
            WidgetType = widget.WidgetType,
            WidthCols = widget.WidthCols,
            DisplayOrder = widget.DisplayOrder
        };

        // Requête de base pour les transactions dans l'intervalle (prise en compte de la date budgétaire décalée)
        var query = dbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.BankAccount)
            .Where(t => (t.BudgetDate ?? t.Date) >= startDate && (t.BudgetDate ?? t.Date) <= endDate);

        if (config.AccountIds != null && config.AccountIds.Count > 0)
        {
            query = query.Where(t => config.AccountIds.Contains(t.BankAccountId));
        }

        if (config.CategoryIds != null && config.CategoryIds.Count > 0)
        {
            query = query.Where(t => t.CategoryId != null && config.CategoryIds.Contains(t.CategoryId.Value));
        }

        var transactions = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        // Récupérer les traductions des catégories concernées
        var catIds = transactions.Select(t => t.Category).OfType<Ledger.Features.Categories.Entities.Category>().Select(c => c.Id).Distinct().ToList();
        var catTranslations = await localizedEntityService.GetTranslationsAsync("Category", catIds, "Name", cancellationToken: cancellationToken).ConfigureAwait(false);

        switch (widget.WidgetType)
        {
            case "KpiExpenses":
                var totalExp = transactions.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);
                result.KpiValue = totalExp;
                result.KpiFormattedValue = totalExp.ToString("C2", CultureInfo.CurrentCulture);
                result.KpiSubtext = periodLabel;
                result.KpiIcon = "bi-arrow-down-circle";
                result.KpiColor = "#ef4444";
                break;

            case "KpiIncome":
                var totalInc = transactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
                result.KpiValue = totalInc;
                result.KpiFormattedValue = totalInc.ToString("C2", CultureInfo.CurrentCulture);
                result.KpiSubtext = periodLabel;
                result.KpiIcon = "bi-arrow-up-circle";
                result.KpiColor = "#10b981";
                break;

            case "KpiSavings":
                var exp = transactions.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);
                var inc = transactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
                var net = inc - exp;
                result.KpiValue = net;
                result.KpiFormattedValue = (net >= 0 ? "+" : "") + net.ToString("C2", CultureInfo.CurrentCulture);
                result.KpiSubtext = periodLabel;
                result.KpiIcon = "bi-piggy-bank";
                result.KpiColor = net >= 0 ? "#10b981" : "#ef4444";
                break;

            case "KpiRemainingToLive":
                var now = DateTime.Today;
                var summaryR = await recurringService.GetMonthlySummaryAsync(now.Year, now.Month, cancellationToken).ConfigureAwait(false);
                result.KpiValue = summaryR.EstimatedRemainingToLive;
                result.KpiFormattedValue = summaryR.EstimatedRemainingToLive.ToString("C2", CultureInfo.CurrentCulture);
                result.KpiSubtext = string.Format(CultureInfo.CurrentCulture, localizer["Reports.IncomeMinusFixedExpenses"], summaryR.TotalIncome.ToString("C0", CultureInfo.CurrentCulture), summaryR.TotalExpected.ToString("C0", CultureInfo.CurrentCulture));
                result.KpiIcon = "bi-cash-coin";
                result.KpiColor = summaryR.EstimatedRemainingToLive >= 0 ? "#3b82f6" : "#f59e0b";
                break;

            case "ChartDoughnutExpenses":
                ComputeCategoryDoughnut(result, transactions, TransactionType.Debit, catTranslations);
                break;

            case "ChartDoughnutIncome":
                ComputeCategoryDoughnut(result, transactions, TransactionType.Credit, catTranslations);
                break;

            case "ChartBarMonthly":
                await ComputeMonthlyComparisonBarAsync(result).ConfigureAwait(false);
                break;

            case "ChartLineSavings":
                await ComputeCumulativeSavingsLineAsync(result).ConfigureAwait(false);
                break;

            case "TopExpensesList":
                ComputeTopExpensesList(result, transactions, catTranslations, config.TopCount);
                break;

            case "RecurringGauges":
                var today = DateTime.Today;
                var monthlyRec = await recurringService.GetMonthlySummaryAsync(today.Year, today.Month, cancellationToken).ConfigureAwait(false);
                result.RecurringExpected = monthlyRec.TotalExpected;
                result.RecurringDebited = monthlyRec.TotalDebited;
                result.RecurringPending = monthlyRec.TotalPending;
                result.RecurringProgressPercent = monthlyRec.ProgressPercentage;
                result.KpiSubtext = $"{monthlyRec.Items.Count(i => i.Status == Recurring.Models.RecurringMonthStatus.Debited)}/{monthlyRec.Items.Count} {localizer["Reports.DebitedSoFar"]}";
                break;
        }

        return result;
    }

    private void ComputeCategoryDoughnut(WidgetDataDto result, List<Transaction> transactions, TransactionType type, Dictionary<Guid, string> catTranslations)
    {
        result.ChartType = "doughnut";
        var filtered = transactions.Where(t => t.Type == type).ToList();

        var groups = filtered
            .GroupBy(t => t.Category)
            .Select(g =>
            {
                string name;
                string color;
                if (g.Key != null)
                {
                    name = catTranslations.TryGetValue(g.Key.Id, out var tr) && !string.IsNullOrWhiteSpace(tr) ? tr : g.Key.Name;
                    color = g.Key.ColorHex;
                }
                else
                {
                    name = localizer["Common.Uncategorized"].Value;
                    color = "#94a3b8";
                }
                return new
                {
                    Name = name,
                    Color = color,
                    Total = g.Sum(t => t.Amount)
                };
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        var dataset = new ChartDatasetDto
        {
            Label = type == TransactionType.Debit ? localizer["Reports.Expenses"].Value : localizer["Reports.Income"].Value
        };

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            result.Labels.Add(g.Name);
            dataset.Data.Add(Math.Round(g.Total, 2));
            dataset.BackgroundColor.Add(!string.IsNullOrEmpty(g.Color) ? g.Color : DefaultPalette[i % DefaultPalette.Length]);
            dataset.BorderColor.Add("#ffffff");
        }

        result.Datasets.Add(dataset);
    }

    private async Task ComputeMonthlyComparisonBarAsync(WidgetDataDto result)
    {
        result.ChartType = "bar";

        // Historique sur les 6 derniers mois par défaut
        int monthsCount = 6;
        var endMonth = DateTime.UtcNow;
        var startMonth = new DateTime(endMonth.Year, endMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(monthsCount - 1));
        var endOfPeriod = new DateTime(endMonth.Year, endMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddTicks(-1);

        var transactions = await dbContext.Transactions
            .Where(t => (t.BudgetDate ?? t.Date) >= startMonth && (t.BudgetDate ?? t.Date) <= endOfPeriod)
            .ToListAsync().ConfigureAwait(false);

        var expenseDataset = new ChartDatasetDto
        {
            Label = localizer["Reports.Expenses"].Value,
            BackgroundColor = new List<string> { "#ef4444" }
        };

        var incomeDataset = new ChartDatasetDto
        {
            Label = localizer["Reports.Income"].Value,
            BackgroundColor = new List<string> { "#10b981" }
        };

        var culture = CultureInfo.CurrentUICulture;

        for (int i = 0; i < monthsCount; i++)
        {
            var mDate = startMonth.AddMonths(i);
            var mStart = new DateTime(mDate.Year, mDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var mEnd = mStart.AddMonths(1).AddTicks(-1);

            var monthTxs = transactions.Where(t => (t.BudgetDate ?? t.Date) >= mStart && (t.BudgetDate ?? t.Date) <= mEnd).ToList();
            var exp = monthTxs.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);
            var inc = monthTxs.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);

            result.Labels.Add(mDate.ToString("MMM yyyy", culture));
            expenseDataset.Data.Add(Math.Round(exp, 2));
            incomeDataset.Data.Add(Math.Round(inc, 2));
        }

        result.Datasets.Add(incomeDataset);
        result.Datasets.Add(expenseDataset);
    }

    private async Task ComputeCumulativeSavingsLineAsync(WidgetDataDto result)
    {
        result.ChartType = "line";

        int monthsCount = 6;
        var endMonth = DateTime.UtcNow;
        var startMonth = new DateTime(endMonth.Year, endMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(monthsCount - 1));
        var endOfPeriod = new DateTime(endMonth.Year, endMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddTicks(-1);

        var transactions = await dbContext.Transactions
            .Where(t => (t.BudgetDate ?? t.Date) >= startMonth && (t.BudgetDate ?? t.Date) <= endOfPeriod)
            .ToListAsync().ConfigureAwait(false);

        var savingsDataset = new ChartDatasetDto
        {
            Label = localizer["Reports.NetSavings"].Value,
            BackgroundColor = new List<string> { "rgba(59, 130, 246, 0.15)" },
            BorderColor = new List<string> { "#3b82f6" },
            Fill = true,
            BorderWidth = 2,
            Tension = 0.3m
        };

        var culture = CultureInfo.CurrentUICulture;

        for (int i = 0; i < monthsCount; i++)
        {
            var mDate = startMonth.AddMonths(i);
            var mStart = new DateTime(mDate.Year, mDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var mEnd = mStart.AddMonths(1).AddTicks(-1);

            var monthTxs = transactions.Where(t => (t.BudgetDate ?? t.Date) >= mStart && (t.BudgetDate ?? t.Date) <= mEnd).ToList();
            var exp = monthTxs.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);
            var inc = monthTxs.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
            var net = inc - exp;

            result.Labels.Add(mDate.ToString("MMM yyyy", culture));
            savingsDataset.Data.Add(Math.Round(net, 2));
        }

        result.Datasets.Add(savingsDataset);
    }

    private void ComputeTopExpensesList(WidgetDataDto result, List<Transaction> transactions, Dictionary<Guid, string> catTranslations, int topCount)
    {
        var debitTxs = transactions.Where(t => t.Type == TransactionType.Debit).ToList();
        var totalAmount = debitTxs.Sum(t => t.Amount);

        var groups = debitTxs
            .GroupBy(t => t.Category)
            .Select(g =>
            {
                string name;
                string icon = "bi-tag";
                string color = "#64748b";

                if (g.Key != null)
                {
                    name = catTranslations.TryGetValue(g.Key.Id, out var tr) && !string.IsNullOrWhiteSpace(tr) ? tr : g.Key.Name;
                    icon = g.Key.IconName;
                    color = g.Key.ColorHex;
                }
                else
                {
                    name = localizer["Common.Uncategorized"].Value;
                }

                var sum = g.Sum(t => t.Amount);
                var pct = totalAmount > 0 ? (sum / totalAmount) * 100 : 0;

                return new TableRowDto
                {
                    Label = name,
                    IconName = icon,
                    ColorHex = color,
                    Amount = sum,
                    Percentage = Math.Round(pct, 1),
                    TransactionsCount = g.Count()
                };
            })
            .OrderByDescending(r => r.Amount)
            .Take(topCount > 0 ? topCount : 5)
            .ToList();

        result.TableRows = groups;
    }

    private (DateTime start, DateTime end, string label) ResolveDateRange(string period)
    {
        var today = DateTime.Today;
        var firstOfCurrentMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return period.ToLowerInvariant() switch
        {
            "last_month" => (
                firstOfCurrentMonth.AddMonths(-1),
                firstOfCurrentMonth.AddTicks(-1),
                localizer["Reports.PeriodLastMonth"].Value
            ),
            "last_3_months" => (
                firstOfCurrentMonth.AddMonths(-2),
                firstOfCurrentMonth.AddMonths(1).AddTicks(-1),
                localizer["Reports.PeriodLast3Months"].Value
            ),
            "last_6_months" => (
                firstOfCurrentMonth.AddMonths(-5),
                firstOfCurrentMonth.AddMonths(1).AddTicks(-1),
                localizer["Reports.PeriodLast6Months"].Value
            ),
            "last_12_months" => (
                firstOfCurrentMonth.AddMonths(-11),
                firstOfCurrentMonth.AddMonths(1).AddTicks(-1),
                localizer["Reports.PeriodLast12Months"].Value
            ),
            "current_year" => (
                new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(today.Year, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc),
                localizer["Reports.PeriodCurrentYear"].Value
            ),
            _ => (
                firstOfCurrentMonth,
                firstOfCurrentMonth.AddMonths(1).AddTicks(-1),
                localizer["Reports.PeriodCurrentMonth"].Value
            )
        };
    }

    private static WidgetConfigDto ParseConfig(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new WidgetConfigDto();
        try
        {
            return JsonSerializer.Deserialize<WidgetConfigDto>(json) ?? new WidgetConfigDto();
        }
        catch (JsonException)
        {
            // Repli gracieux : si la configuration du widget est mal formée, instancier une configuration par défaut
            return new WidgetConfigDto();
        }
    }

    private async Task SeedDefaultDashboardAsync(CancellationToken cancellationToken = default)
    {
        var dashboard = new ReportDashboard
        {
            UserId = string.Empty,
            Name = localizer["Reports.MainDashboardName"].Value,
            Description = localizer["Reports.MainDashboardDesc"].Value,
            IsDefault = true,
            DisplayOrder = 1
        };

        dbContext.ReportDashboards.Add(dashboard);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var widgets = new List<ReportWidget>
        {
            new() { DashboardId = dashboard.Id, Title = localizer["Reports.KpiExpensesTitle"].Value, WidgetType = "KpiExpenses", WidthCols = 3, DisplayOrder = 1, ConfigJson = "{\"Period\":\"current_month\"}" },
            new() { DashboardId = dashboard.Id, Title = localizer["Reports.KpiIncomeTitle"].Value, WidgetType = "KpiIncome", WidthCols = 3, DisplayOrder = 2, ConfigJson = "{\"Period\":\"current_month\"}" },
            new() { DashboardId = dashboard.Id, Title = localizer["Reports.KpiSavingsTitle"].Value, WidgetType = "KpiSavings", WidthCols = 3, DisplayOrder = 3, ConfigJson = "{\"Period\":\"current_month\"}" },
            new() { DashboardId = dashboard.Id, Title = localizer["Reports.KpiRemainingToLiveTitle"].Value, WidgetType = "KpiRemainingToLive", WidthCols = 3, DisplayOrder = 4, ConfigJson = "{\"Period\":\"current_month\"}" },
            new() { DashboardId = dashboard.Id, Title = localizer["Reports.CategoryDoughnutTitle"].Value, WidgetType = "ChartDoughnutExpenses", WidthCols = 6, DisplayOrder = 5, ConfigJson = "{\"Period\":\"current_month\"}" },
            new() { DashboardId = dashboard.Id, Title = localizer["Reports.RecurringGaugesTitle"].Value, WidgetType = "RecurringGauges", WidthCols = 6, DisplayOrder = 6, ConfigJson = "{\"Period\":\"current_month\"}" },
            new() { DashboardId = dashboard.Id, Title = localizer["Reports.MonthlyBarTitle"].Value, WidgetType = "ChartBarMonthly", WidthCols = 8, DisplayOrder = 7, ConfigJson = "{\"Period\":\"last_6_months\"}" },
            new() { DashboardId = dashboard.Id, Title = localizer["Reports.TopExpensesTitle"].Value, WidgetType = "TopExpensesList", WidthCols = 4, DisplayOrder = 8, ConfigJson = "{\"Period\":\"current_month\",\"TopCount\":5}" }
        };

        dbContext.ReportWidgets.AddRange(widgets);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
