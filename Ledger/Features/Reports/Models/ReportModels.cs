namespace Ledger.Features.Reports.Models;

public class WidgetConfigDto
{
    public string Period { get; set; } = "current_month"; // current_month, last_month, last_3_months, last_6_months, last_12_months, current_year
    public List<Guid>? AccountIds { get; set; }
    public List<Guid>? CategoryIds { get; set; }
    public bool ShowLegend { get; set; } = true;
    public int TopCount { get; set; } = 5;
}

public class ChartDatasetDto
{
    public string Label { get; set; } = string.Empty;
    public List<decimal> Data { get; set; } = new();
    public List<string> BackgroundColor { get; set; } = new();
    public List<string> BorderColor { get; set; } = new();
    public int BorderWidth { get; set; } = 1;
    public bool Fill { get; set; } = false;
    public decimal? Tension { get; set; } = 0.3m;
}

public class TableRowDto
{
    public string Label { get; set; } = string.Empty;
    public string? SubLabel { get; set; }
    public string? IconName { get; set; }
    public string? ColorHex { get; set; }
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
    public int TransactionsCount { get; set; }
}

public class WidgetDataDto
{
    public Guid WidgetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string WidgetType { get; set; } = string.Empty;
    public int WidthCols { get; set; } = 6;
    public int DisplayOrder { get; set; } = 1;

    // Métrique pour les cartes KPI
    public decimal? KpiValue { get; set; }
    public string? KpiFormattedValue { get; set; }
    public string? KpiSubtext { get; set; }
    public string? KpiIcon { get; set; }
    public string? KpiColor { get; set; }
    public decimal? KpiTrendPercent { get; set; }

    // Données pour Chart.js
    public string? ChartType { get; set; } // "doughnut", "bar", "line"
    public List<string> Labels { get; set; } = new();
    public List<ChartDatasetDto> Datasets { get; set; } = new();

    // Données pour tableaux / listes
    public List<TableRowDto> TableRows { get; set; } = new();

    // Données pour suivi charges récurrentes
    public decimal? RecurringExpected { get; set; }
    public decimal? RecurringDebited { get; set; }
    public decimal? RecurringPending { get; set; }
    public int? RecurringProgressPercent { get; set; }
}
