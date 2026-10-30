using System.Globalization;
using Ledger.Features.Reports.Models;

namespace Ledger.Features.Reports.Services;

/// <summary>
/// Construit les en-têtes de colonnes d'un tableau croisé pour une période : un par mois, trimestre ou année couvert.
/// </summary>
internal static class PivotColumnHeaderFactory
{
    /// <summary>Un en-tête par mois, clé « yyyy-MM », libellé abrégé dans la culture d'interface.</summary>
    public static List<PivotColumnHeaderDto> Months(DateTime start, DateTime end)
    {
        var cols = new List<PivotColumnHeaderDto>();
        var culture = CultureInfo.CurrentUICulture;
        var cur = new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endMonth = new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        int order = 1;

        while (cur <= endMonth)
        {
            var key = cur.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var monthName = cur.ToString("MMM yyyy", culture);
            monthName = char.ToUpper(monthName[0], culture) + monthName[1..];

            cols.Add(new PivotColumnHeaderDto
            {
                Key = key,
                Label = monthName,
                SortOrder = order++
            });

            cur = cur.AddMonths(1);
        }

        return cols;
    }

    /// <summary>Un en-tête par trimestre chevauchant la période, clé « yyyy-Qn ».</summary>
    public static List<PivotColumnHeaderDto> Quarters(DateTime start, DateTime end)
    {
        var cols = new List<PivotColumnHeaderDto>();
        var curYear = start.Year;
        var endYear = end.Year;
        int order = 1;

        for (int y = curYear; y <= endYear; y++)
        {
            for (int q = 1; q <= 4; q++)
            {
                int qStartMonth = (q - 1) * 3 + 1;
                var qStart = new DateTime(y, qStartMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                var qEnd = qStart.AddMonths(3).AddDays(-1);

                if (qEnd >= start && qStart <= end)
                {
                    var key = $"{y}-Q{q}";
                    cols.Add(new PivotColumnHeaderDto
                    {
                        Key = key,
                        Label = $"T{q} {y}",
                        SortOrder = order++
                    });
                }
            }
        }

        return cols;
    }

    /// <summary>Un en-tête par année couverte.</summary>
    public static List<PivotColumnHeaderDto> Years(DateTime start, DateTime end)
    {
        var cols = new List<PivotColumnHeaderDto>();
        int order = 1;
        for (int y = start.Year; y <= end.Year; y++)
        {
            cols.Add(new PivotColumnHeaderDto
            {
                Key = y.ToString(CultureInfo.InvariantCulture),
                Label = y.ToString(CultureInfo.InvariantCulture),
                SortOrder = order++
            });
        }

        return cols;
    }
}
