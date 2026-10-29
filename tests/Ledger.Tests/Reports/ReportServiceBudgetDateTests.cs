using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Recurring.Services;
using Ledger.Features.Reports.Entities;
using Ledger.Features.Reports.Services;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace Ledger.Tests.Reports;

public sealed class ReportServiceBudgetDateTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly ReportService _reportService;
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _salaryCategoryId = Guid.NewGuid();
    private const string TestUserId = "test-user-id";

    public ReportServiceBudgetDateTests()
    {
        (_context, _connection) = TestDbContextFactory.Create(TestUserId);

        var mockLocalizedService = new Mock<ILocalizedEntityService>();
        mockLocalizedService
            .Setup(s => s.GetTranslationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var mockLocalizer = new Mock<IStringLocalizer>();
        mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));

        var recurringService = new RecurringExpenseService(_context);
        _reportService = new ReportService(_context, mockLocalizedService.Object, recurringService, mockLocalizer.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var bank = new Bank { Id = Guid.NewGuid(), UserId = TestUserId, Name = "Banque Test" };
        var account = new BankAccount
        {
            Id = _bankAccountId,
            UserId = TestUserId,
            BankId = bank.Id,
            Name = "Compte Courant",
            InitialBalance = 1000m
        };

        var catSalary = new Category
        {
            Id = _salaryCategoryId,
            UserId = TestUserId,
            Name = "Salaire",
            IsIncome = true,
            IsSalary = true,
            ColorHex = "#10b981"
        };

        _context.Banks.Add(bank);
        _context.BankAccounts.Add(account);
        _context.Categories.Add(catSalary);
        _context.SaveChanges();
    }

    [Fact]
    public async Task ComputeWidgetDataAsync_KpiIncome_ShouldAccountForShiftedBudgetDate()
    {
        // Transaction réelle le 28 septembre 2026, mais décalée au budget d'octobre 2026
        var shiftedSalary = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
            BudgetDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 3000m,
            Type = TransactionType.Credit,
            Label = "VIREMENT SALAIRE EMPLOYEUR",
            CategoryId = _salaryCategoryId
        };

        // Autre revenu réel en septembre sans décalage
        var septemberBonus = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            BudgetDate = null,
            Amount = 500m,
            Type = TransactionType.Credit,
            Label = "BONUS SEPTEMBRE",
            CategoryId = _salaryCategoryId
        };

        _context.Transactions.AddRange(shiftedSalary, septemberBonus);
        await _context.SaveChangesAsync();

        var dashboard = await _reportService.CreateDashboardAsync("Dashboard Test");
        await _reportService.AddWidgetAsync(
            dashboard.Id,
            "Revenus",
            "KpiIncome",
            widthCols: 3,
            configJson: "{\"Period\":\"current_month\"}" // Si le mois courant est simulé, testons les dates directement
        );

        // Si aujourd'hui est en octobre 2026 :
        // Pour valider précisément sans dépendre du mois de l'horloge système courante,
        // vérifions que ComputeWidgetDataAsync utilise bien BudgetDate ?? Date :
        var septTx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            BankAccountId = _bankAccountId,
            Date = DateTime.Today.AddDays(-2),
            BudgetDate = DateTime.Today.AddMonths(1), // Décalé au mois prochain
            Amount = 2500m,
            Type = TransactionType.Credit,
            Label = "SALAIRE DECALÉ",
            CategoryId = _salaryCategoryId
        };

        var currentTx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            BankAccountId = _bankAccountId,
            Date = DateTime.Today.AddMonths(-1), // Transaction du mois dernier
            BudgetDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1, 0, 0, 0, DateTimeKind.Utc), // Rattachée au mois en cours
            Amount = 2800m,
            Type = TransactionType.Credit,
            Label = "SALAIRE RATTACHÉ CE MOIS",
            CategoryId = _salaryCategoryId
        };

        _context.Transactions.AddRange(septTx, currentTx);
        await _context.SaveChangesAsync();

        var widgetLastMonth = new ReportWidget
        {
            Id = Guid.NewGuid(),
            DashboardId = dashboard.Id,
            Title = "Revenus Mois Dernier",
            WidgetType = "KpiIncome",
            ConfigJson = "{\"Period\":\"last_month\"}"
        };

        var widgetCurrentMonth = new ReportWidget
        {
            Id = Guid.NewGuid(),
            DashboardId = dashboard.Id,
            Title = "Revenus du mois",
            WidgetType = "KpiIncome",
            ConfigJson = "{\"Period\":\"current_month\"}"
        };

        var resultLastMonth = await _reportService.ComputeWidgetDataAsync(widgetLastMonth);
        var resultCurrent = await _reportService.ComputeWidgetDataAsync(widgetCurrentMonth);

        // 1. Pour le mois dernier (septembre) :
        // Le montant de 500€ (bonus de septembre) est inclus.
        // Le salaire de 3000€ (reçu le 28 septembre mais rattaché au 1er octobre) ne doit PAS être comptabilisé en septembre !
        resultLastMonth.KpiValue.Should().Be(500m);

        // 2. Pour le mois en cours (octobre) :
        // Le montant de 3000€ (du 28 septembre mais rattaché à octobre) doit être inclus !
        // Le montant de 2800€ (du mois dernier mais rattaché à octobre) doit être inclus !
        // Le montant de 2500€ (réalisé ce mois mais avec budgetDate au mois prochain) ne doit PAS être inclus !
        resultCurrent.KpiValue.Should().Be(5800m);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
