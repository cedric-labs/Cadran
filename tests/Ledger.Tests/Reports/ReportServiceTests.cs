using System.Globalization;
using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Services;
using Ledger.Features.Reports.Entities;
using Ledger.Features.Reports.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace Ledger.Tests.Reports;

public sealed class ReportServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly SqliteConnection _connection;
    private readonly Mock<ILocalizedEntityService> _mockLocalizedEntityService;
    private readonly Mock<IRecurringExpenseService> _mockRecurringService;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly ReportService _service;

    public ReportServiceTests()
    {
        (_dbContext, _connection) = TestDbContextFactory.Create();

        _mockLocalizedEntityService = new Mock<ILocalizedEntityService>();
        _mockLocalizedEntityService
            .Setup(l => l.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        _mockRecurringService = new Mock<IRecurringExpenseService>();
        _mockRecurringService
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<RecurringExpense>());
        _mockRecurringService
            .Setup(r => r.GetMonthlySummaryAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new Ledger.Features.Recurring.Models.MonthlyRecurringSummaryDto
            {
                TotalExpected = 500m,
                TotalDebited = 300m,
                TotalIncome = 2000m,
                Items = new List<Ledger.Features.Recurring.Models.MonthlyRecurringItemDto>()
            });

        _mockLocalizer = new Mock<IStringLocalizer>();
        _mockLocalizer.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns<string, object[]>((k, args) => new LocalizedString(k, string.Format(CultureInfo.CurrentCulture, k, args)));

        _service = new ReportService(
            _dbContext,
            _mockLocalizedEntityService.Object,
            _mockRecurringService.Object,
            _mockLocalizer.Object,
            TimeProvider.System);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetDashboardsAsync_WhenEmpty_ShouldSeedDefaultDashboard()
    {
        var dashboards = await _service.GetDashboardsAsync();

        dashboards.Should().NotBeEmpty();
        dashboards[0].Widgets.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateDashboardAsync_WhenInvoked_CreatesAndDeletesDashboard()
    {
        var created = await _service.CreateDashboardAsync("Mon Tableau de Bord", "Description test");
        created.Should().NotBeNull();
        created.Name.Should().Be("Mon Tableau de Bord");

        var fetched = await _service.GetDashboardByIdAsync(created.Id);
        fetched.Should().NotBeNull();
        fetched.Name.Should().Be("Mon Tableau de Bord");

        var deleted = await _service.DeleteDashboardAsync(created.Id);
        deleted.Should().BeTrue();

        var afterDelete = await _service.GetDashboardByIdAsync(created.Id);
        afterDelete.Should().BeNull();
    }

    [Fact]
    public async Task ManageWidgetAsync_WhenInvoked_AddsUpdatesAndDeletesWidgets()
    {
        var dashboard = await _service.CreateDashboardAsync("Dashboard Widgets");

        var widget = await _service.AddWidgetAsync(dashboard.Id, "Dépenses du mois", "KpiExpenses", 6, "{\"Period\":\"CurrentMonth\"}");
        widget.Should().NotBeNull();
        widget.Title.Should().Be("Dépenses du mois");

        var updated = await _service.UpdateWidgetAsync(widget.Id, "Dépenses révisées", 12, "{}");
        updated.Should().NotBeNull();
        updated.Title.Should().Be("Dépenses révisées");
        updated.WidthCols.Should().Be(12);

        var deleted = await _service.DeleteWidgetAsync(widget.Id);
        deleted.Should().BeTrue();
    }

    [Fact]
    public async Task ComputeWidgetDataAsync_Kpis_ShouldCalculateCorrectTotals()
    {
        var bank = new Bank { UserId = "test-user-id", Name = "Banque Test" };
        _dbContext.Banks.Add(bank);

        var account = new BankAccount
        {
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Courant",
            Type = AccountType.Checking,
            InitialBalance = 0m
        };
        _dbContext.BankAccounts.Add(account);

        var cat = new Category { UserId = "test-user-id", Name = "Courses", ColorHex = "#3b82f6" };
        _dbContext.Categories.Add(cat);

        // Transactions du mois courant
        var now = DateTime.UtcNow;
        var firstOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _dbContext.Transactions.AddRange(
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                CategoryId = cat.Id,
                Date = firstOfMonth.AddDays(1),
                Amount = 100m,
                Type = TransactionType.Debit,
                Label = "Supermarché"
            },
            new Transaction
            {
                UserId = "test-user-id",
                BankAccountId = account.Id,
                Date = firstOfMonth.AddDays(2),
                Amount = 250m,
                Type = TransactionType.Credit,
                Label = "Virement Reçu"
            }
        );
        await _dbContext.SaveChangesAsync();

        var dashboard = await _service.CreateDashboardAsync("Dashboard Calculs");

        // KPI Dépenses
        var kpiExpWidget = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Dépenses",
            WidgetType = "KpiExpenses",
            WidthCols = 6,
            ConfigJson = "{\"Period\":\"CurrentMonth\"}"
        };

        var expResult = await _service.ComputeWidgetDataAsync(kpiExpWidget);
        expResult.KpiValue.Should().Be(100m);

        // KPI Revenus
        var kpiIncWidget = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Revenus",
            WidgetType = "KpiIncome",
            WidthCols = 6,
            ConfigJson = "{\"Period\":\"CurrentMonth\"}"
        };

        var incResult = await _service.ComputeWidgetDataAsync(kpiIncWidget);
        incResult.KpiValue.Should().Be(250m);

        // KPI Épargne nette
        var kpiSavWidget = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Épargne",
            WidgetType = "KpiSavings",
            WidthCols = 6,
            ConfigJson = "{\"Period\":\"CurrentMonth\"}"
        };

        var savResult = await _service.ComputeWidgetDataAsync(kpiSavWidget);
        savResult.KpiValue.Should().Be(150m); // 250 - 100

        // Graphique Répartition Catégories
        var chartWidget = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Répartition",
            WidgetType = "ChartDoughnutExpenses",
            WidthCols = 6,
            ConfigJson = "{\"Period\":\"CurrentMonth\"}"
        };

        var chartResult = await _service.ComputeWidgetDataAsync(chartWidget);
        chartResult.Labels.Should().Contain("Courses");
    }

    [Fact]
    public async Task ComputeWidgetDataAsync_WithAllRemainingWidgetTypesAndBranches_CalculatesCorrectly()
    {
        var bank = new Bank { UserId = "test-user-id", Name = "Banque Test 2" };
        _dbContext.Banks.Add(bank);

        var account1 = new BankAccount { UserId = "test-user-id", BankId = bank.Id, Name = "Compte A", Type = AccountType.Checking, InitialBalance = 0m };
        var account2 = new BankAccount { UserId = "test-user-id", BankId = bank.Id, Name = "Compte B", Type = AccountType.Checking, InitialBalance = 0m };
        _dbContext.BankAccounts.AddRange(account1, account2);

        var catIncome = new Category { UserId = "test-user-id", Name = "Salaire", ColorHex = "#10b981", IsIncome = true };
        var catWithoutColor = new Category { UserId = "test-user-id", Name = "Divers", ColorHex = "", IsIncome = false };
        _dbContext.Categories.AddRange(catIncome, catWithoutColor);

        var today = DateTime.Today;
        var firstOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Transactions avec et sans catégorie, dépenses et revenus
        _dbContext.Transactions.AddRange(
            // Dépense avec catégorie sans couleur
            new Transaction { UserId = "test-user-id", BankAccountId = account1.Id, CategoryId = catWithoutColor.Id, Date = firstOfMonth.AddDays(1), Amount = 500m, Type = TransactionType.Debit, Label = "Achat divers" },
            // Dépense non catégorisée
            new Transaction { UserId = "test-user-id", BankAccountId = account1.Id, CategoryId = null, Date = firstOfMonth.AddDays(2), Amount = 300m, Type = TransactionType.Debit, Label = "Retrait DAB" },
            // Revenu catégorisé
            new Transaction { UserId = "test-user-id", BankAccountId = account2.Id, CategoryId = catIncome.Id, Date = firstOfMonth.AddDays(3), Amount = 400m, Type = TransactionType.Credit, Label = "Virement salaire" },
            // Revenu non catégorisé
            new Transaction { UserId = "test-user-id", BankAccountId = account2.Id, CategoryId = null, Date = firstOfMonth.AddDays(4), Amount = 100m, Type = TransactionType.Credit, Label = "Remboursement" },
            // Transaction passée (il y a 2 mois) pour tester l'historique mensuel
            new Transaction { UserId = "test-user-id", BankAccountId = account1.Id, Date = firstOfMonth.AddMonths(-2).AddDays(5), Amount = 120m, Type = TransactionType.Debit, Label = "Facture passée" }
        );
        await _dbContext.SaveChangesAsync();

        var dashboard = await _service.CreateDashboardAsync("Dashboard Complet");

        // Mock du résumé récurrent
        _mockRecurringService
            .Setup(r => r.GetMonthlySummaryAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new Ledger.Features.Recurring.Models.MonthlyRecurringSummaryDto
            {
                Year = today.Year,
                Month = today.Month,
                TotalExpected = 800m,
                TotalDebited = 500m,
                TotalPending = 300m,
                TotalIncome = 2500m,
                Items = new List<Ledger.Features.Recurring.Models.MonthlyRecurringItemDto>
                {
                    new() { Status = Ledger.Features.Recurring.Models.RecurringMonthStatus.Debited, ExpectedAmount = 500m },
                    new() { Status = Ledger.Features.Recurring.Models.RecurringMonthStatus.Pending, ExpectedAmount = 300m }
                }
            });

        // 1. KPI Épargne Négative (dépenses 800 > revenus 500 dans le mois)
        var kpiSavNegative = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Épargne Négative",
            WidgetType = "KpiSavings",
            ConfigJson = "{\"Period\":\"current_month\"}"
        };
        var resSav = await _service.ComputeWidgetDataAsync(kpiSavNegative);
        resSav.KpiValue.Should().Be(-300m);
        resSav.KpiColor.Should().Be("#ef4444");
        resSav.KpiFormattedValue.Should().NotStartWith("+");

        // 2. KpiRemainingToLive
        var kpiLive = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Reste à vivre",
            WidgetType = "KpiRemainingToLive",
            ConfigJson = "{}"
        };
        var resLive = await _service.ComputeWidgetDataAsync(kpiLive);
        resLive.KpiValue.Should().Be(1700m);
        resLive.KpiColor.Should().Be("#3b82f6");

        // 3. RecurringGauges
        var widgetGauges = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Jauges Récurrentes",
            WidgetType = "RecurringGauges",
            ConfigJson = "{}"
        };
        var resGauges = await _service.ComputeWidgetDataAsync(widgetGauges);
        resGauges.RecurringExpected.Should().Be(800m);
        resGauges.RecurringDebited.Should().Be(500m);
        resGauges.RecurringPending.Should().Be(300m);

        // 4. ChartDoughnutIncome (incluant non catégorisé)
        var chartIncome = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Revenus Répartition",
            WidgetType = "ChartDoughnutIncome",
            ConfigJson = "{\"Period\":\"current_month\"}"
        };
        var resIncDoughnut = await _service.ComputeWidgetDataAsync(chartIncome);
        resIncDoughnut.ChartType.Should().Be("doughnut");
        resIncDoughnut.Labels.Should().Contain("Salaire");
        resIncDoughnut.Labels.Should().Contain("Common.Uncategorized");

        // 5. ChartBarMonthly
        var chartBar = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Comparaison Mensuelle",
            WidgetType = "ChartBarMonthly",
            ConfigJson = "{\"Period\":\"last_6_months\"}"
        };
        var resBar = await _service.ComputeWidgetDataAsync(chartBar);
        resBar.ChartType.Should().Be("bar");
        resBar.Datasets.Should().HaveCount(2);

        // 6. ChartLineSavings
        var chartLine = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Épargne Cumulée",
            WidgetType = "ChartLineSavings",
            ConfigJson = "{\"Period\":\"last_6_months\"}"
        };
        var resLine = await _service.ComputeWidgetDataAsync(chartLine);
        resLine.ChartType.Should().Be("line");
        resLine.Datasets.Should().HaveCount(1);

        // 7. TopExpensesList avec topCount configuré et non catégorisé
        var topWidget = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Top Dépenses",
            WidgetType = "TopExpensesList",
            ConfigJson = "{\"Period\":\"current_month\",\"TopCount\":3}"
        };
        var resTop = await _service.ComputeWidgetDataAsync(topWidget);
        resTop.TableRows.Should().NotBeEmpty();
        resTop.TableRows.Any(r => string.Equals(r.Label, "Common.Uncategorized", StringComparison.Ordinal)).Should().BeTrue();

        // 8. TopExpensesList avec topCount <= 0 (repli sur 5)
        var topWidgetFallback = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Top Dépenses Fallback",
            WidgetType = "TopExpensesList",
            ConfigJson = "{\"Period\":\"current_month\",\"TopCount\":0}"
        };
        var resTopFallback = await _service.ComputeWidgetDataAsync(topWidgetFallback);
        resTopFallback.TableRows.Should().NotBeEmpty();

        // 9. Filtrage par AccountIds et CategoryIds
        var filteredWidget = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Dépenses Filtrées",
            WidgetType = "KpiExpenses",
            ConfigJson = $"{{\"Period\":\"current_month\",\"AccountIds\":[\"{account1.Id}\"],\"CategoryIds\":[\"{catWithoutColor.Id}\"]}}"
        };
        var resFiltered = await _service.ComputeWidgetDataAsync(filteredWidget);
        resFiltered.KpiValue.Should().Be(500m);

        // 10. Périodes de date différentes
        foreach (var period in new[] { "last_month", "last_3_months", "last_6_months", "last_12_months", "current_year", "unknown_period" })
        {
            var periodWidget = new ReportWidget
            {
                DashboardId = dashboard.Id,
                Title = $"Période {period}",
                WidgetType = "KpiExpenses",
                ConfigJson = $"{{\"Period\":\"{period}\"}}"
            };
            var resP = await _service.ComputeWidgetDataAsync(periodWidget);
            resP.Should().NotBeNull();
        }

        // 11. ConfigJson invalide ou vide
        var invalidConfigWidget = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Config Invalide",
            WidgetType = "KpiExpenses",
            ConfigJson = "{ non valid json }"
        };
        var resInvalid = await _service.ComputeWidgetDataAsync(invalidConfigWidget);
        resInvalid.Should().NotBeNull();

        var emptyConfigWidget = new ReportWidget
        {
            DashboardId = dashboard.Id,
            Title = "Config Vide",
            WidgetType = "KpiExpenses",
            ConfigJson = ""
        };
        var resEmpty = await _service.ComputeWidgetDataAsync(emptyConfigWidget);
        resEmpty.Should().NotBeNull();
    }

    [Fact]
    public async Task ComputeDashboardDataAsync_WhenInvoked_ComputesDataAndResetsDashboard()
    {
        // 1. Initialiser le dashboard par défaut
        var dashboards = await _service.GetDashboardsAsync();
        var defaultDashboard = dashboards.First(d => d.IsDefault);

        // 2. Calculer l'ensemble des widgets du dashboard
        var dashboardData = await _service.ComputeDashboardDataAsync(defaultDashboard.Id);
        dashboardData.Should().NotBeNull();
        dashboardData.Should().NotBeEmpty();

        // 3. Réinitialiser le dashboard par défaut
        await _service.ResetDefaultDashboardAsync();

        var afterReset = await _service.GetDashboardsAsync();
        afterReset.Should().NotBeEmpty();
        afterReset.First(d => d.IsDefault).Widgets.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ComputeWidgetDataAsync_WhenTodayIsFixed_UsesCurrentMonthPeriod()
    {
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2025, 3, 15, 10, 30, 0, TimeSpan.Zero));
        var service = new ReportService(
            _dbContext,
            _mockLocalizedEntityService.Object,
            _mockRecurringService.Object,
            _mockLocalizer.Object,
            fakeTime);

        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque Test" };
        var account = new BankAccount { Id = Guid.NewGuid(), UserId = "test-user-id", BankId = bank.Id, Name = "Compte Test" };
        _dbContext.Banks.Add(bank);
        _dbContext.BankAccounts.Add(account);
        _dbContext.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = account.Id,
            Date = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            Amount = 142.50m,
            Type = TransactionType.Debit,
            Label = "Dépense Mars 2025"
        });
        await _dbContext.SaveChangesAsync();

        var widget = new ReportWidget
        {
            DashboardId = Guid.NewGuid(),
            Title = "Dépenses du mois",
            WidgetType = "KpiExpenses",
            ConfigJson = "{\"Period\":\"current_month\"}"
        };

        var result = await service.ComputeWidgetDataAsync(widget);

        result.KpiValue.Should().Be(142.50m);
    }
}
