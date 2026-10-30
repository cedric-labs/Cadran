using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Features.Recurring.Models;
using Ledger.Features.Recurring.Services;
using Ledger.Tests.Fixtures;
using Xunit;

namespace Ledger.Tests.Recurring;

public sealed class RecurringExpenseServiceTests : IDisposable
{
    private static readonly string[] ExpectedSpotifyNames = ["Spotify Solo", "Spotify Famille"];
    private readonly Ledger.Infrastructure.Data.AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly RecurringExpenseService _recurringService;
    private readonly Guid _bankAccountId = Guid.NewGuid();

    public RecurringExpenseServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _recurringService = new RecurringExpenseService(_context, TimeProvider.System);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var bank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Banque Populaire" };
        var account = new BankAccount
        {
            Id = _bankAccountId,
            UserId = "test-user-id",
            BankId = bank.Id,
            Name = "Compte Principal"
        };

        var catSubs = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Abonnements",
            ColorHex = "#8b5cf6"
        };

        // Simuler 4 prélèvements mensuels Netflix réguliers dans les 12 derniers mois
        var today = DateTime.Today;
        var baseDate = new DateTime(today.Year, today.Month, 10, 0, 0, 0, DateTimeKind.Utc).AddMonths(-4);
        var txList = new List<Transaction>();

        for (int i = 0; i < 4; i++)
        {
            txList.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = "test-user-id",
                BankAccountId = _bankAccountId,
                Date = baseDate.AddMonths(i),
                Amount = 17.99m,
                Type = TransactionType.Debit,
                Label = "PRLV SEPA NETFLIX SERVICES",
                CategoryId = catSubs.Id
            });
        }

        _context.Banks.Add(bank);
        _context.BankAccounts.Add(account);
        _context.Categories.Add(catSubs);
        _context.Transactions.AddRange(txList);
        _context.SaveChanges();
    }

    [Fact]
    public async Task DetectCandidatesAsync_WithRegularMonthlyDebits_ShouldDetectNetflixCandidate()
    {
        // Act
        var candidates = await _recurringService.DetectCandidatesAsync();

        // Assert
        candidates.Should().NotBeEmpty();
        var netflix = candidates.FirstOrDefault(c => c.SuggestedName.Contains("NETFLIX", StringComparison.OrdinalIgnoreCase));
        netflix.Should().NotBeNull();
        netflix.OccurrencesCount.Should().Be(4);
        netflix.AverageAmount.Should().Be(17.99m);
        netflix.ExpectedDayOfMonth.Should().Be(10);
        netflix.Frequency.Should().Be(RecurringFrequency.Monthly);
        netflix.ConfidenceScore.Should().BeGreaterThan(80);
        netflix.IsAlreadyConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmCandidateAsync_WhenCalledMultipleTimes_ShouldBeIdempotentWithoutDuplicates()
    {
        // Arrange
        var candidate = new RecurringCandidateDto
        {
            SuggestedName = "Netflix France",
            Pattern = "NETFLIX",
            AverageAmount = 17.99m,
            LastAmount = 17.99m,
            Frequency = RecurringFrequency.Monthly,
            ExpectedDayOfMonth = 10,
            LastDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act 1
        var confirmed1 = await _recurringService.ConfirmCandidateAsync(candidate);
        var countAfterFirst = _context.RecurringExpenses.Count();

        // Act 2 (Re-confirmation du même candidat)
        var confirmed2 = await _recurringService.ConfirmCandidateAsync(candidate);
        var countAfterSecond = _context.RecurringExpenses.Count();

        // Assert
        confirmed1.Should().NotBeNull();
        confirmed2.Should().NotBeNull();
        confirmed2.Id.Should().Be(confirmed1.Id);
        countAfterFirst.Should().Be(1);
        countAfterSecond.Should().Be(1); // Aucun doublon créé !
    }

    [Fact]
    public async Task IgnoreCandidateAsync_WhenCalled_PersistsPatternAndExcludesCandidate()
    {
        // 1. Initialement, Netflix doit être détecté
        var initialCandidates = await _recurringService.DetectCandidatesAsync();
        initialCandidates.Should().Contain(c => c.Pattern.Contains("NETFLIX", StringComparison.OrdinalIgnoreCase));

        // 2. Ignorer le candidat Netflix
        await _recurringService.IgnoreCandidateAsync("NETFLIX");

        // Vérifier la persistance dans AppSettings
        var ignoredPatterns = await _recurringService.GetIgnoredPatternsAsync();
        ignoredPatterns.Should().Contain("NETFLIX");

        // 3. Après avoir été ignoré, DetectCandidatesAsync ne doit plus le renvoyer
        var candidatesAfterIgnore = await _recurringService.DetectCandidatesAsync();
        candidatesAfterIgnore.Should().NotContain(c => c.Pattern.Contains("NETFLIX", StringComparison.OrdinalIgnoreCase));

        // 4. Si on réactive (unignore), il doit réapparaître
        await _recurringService.UnignoreCandidateAsync("NETFLIX");
        var candidatesAfterUnignore = await _recurringService.DetectCandidatesAsync();
        candidatesAfterUnignore.Should().Contain(c => c.Pattern.Contains("NETFLIX", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_WhenCalled_IncludesIncomeWithShiftedBudgetDate()
    {
        // Arrange
        var catSalary = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Salaire",
            IsIncome = true,
            IsSalary = true
        };
        _context.Categories.Add(catSalary);

        // Transaction passée le 28 du mois précédent mais avec BudgetDate au mois cible (2026-11)
        var shiftedSalary = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc),
            BudgetDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 3200m,
            Type = TransactionType.Credit,
            Label = "VIREMENT SALAIRE",
            CategoryId = catSalary.Id
        };

        // Transaction passée en novembre mais avec BudgetDate décalée en décembre (2026-12)
        var shiftedAwayIncome = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc),
            BudgetDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 500m,
            Type = TransactionType.Credit,
            Label = "PRIME FIN D'ANNEE",
            CategoryId = catSalary.Id
        };

        _context.Transactions.AddRange(shiftedSalary, shiftedAwayIncome);
        await _context.SaveChangesAsync();

        // Act
        var summary = await _recurringService.GetMonthlySummaryAsync(2026, 11);

        // Assert
        summary.TotalIncome.Should().Be(3200m);
        summary.SalaryIncome.Should().Be(3200m);
    }

    [Fact]
    public async Task GetMatchesForTransactionsAsync_ShouldReturnSingleMatch_WhenOneRecurringExpenseMatches()
    {
        // Arrange
        var expense = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Netflix",
            Pattern = "NETFLIX",
            ExpectedAmount = 17.99m,
            IsActive = true
        };
        _context.RecurringExpenses.Add(expense);
        await _context.SaveChangesAsync();

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 17.99m,
            Label = "PRLV SEPA NETFLIX SERVICES",
            RawLabel = "NETFLIX LUXEMBOURG"
        };

        // Act
        var matches = await _recurringService.GetMatchesForTransactionsAsync(new[] { tx });

        // Assert
        matches.Should().ContainKey(tx.Id);
        var match = matches[tx.Id];
        match.MatchCount.Should().Be(1);
        match.FirstExpenseName.Should().Be("Netflix");
        match.MatchingExpenseNames.Should().ContainSingle().Which.Should().Be("Netflix");
    }

    [Fact]
    public async Task GetMatchesForTransactionsAsync_ShouldReturnMultipleMatches_WhenMultipleExpensesMatch()
    {
        // Arrange
        var exp1 = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Spotify Solo",
            Pattern = "SPOTIFY",
            ExpectedAmount = 17.99m,
            IsActive = true
        };
        var exp2 = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Spotify Famille",
            Pattern = "SPOTIFY FAMILY",
            ExpectedAmount = 17.99m,
            IsActive = true
        };
        _context.RecurringExpenses.AddRange(exp1, exp2);
        await _context.SaveChangesAsync();

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 17.99m,
            Label = "PRLV SPOTIFY FAMILY ABONNEMENT"
        };

        // Act
        var matches = await _recurringService.GetMatchesForTransactionsAsync(new[] { tx });

        // Assert
        matches.Should().ContainKey(tx.Id);
        var match = matches[tx.Id];
        match.MatchCount.Should().Be(2);
        match.MatchingExpenseNames.Should().BeEquivalentTo(ExpectedSpotifyNames);
    }

    [Fact]
    public async Task GetMatchesForTransactionsAsync_ShouldReturnZeroMatches_WhenNoExpenseMatches()
    {
        // Arrange
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 4.50m,
            Label = "BOULANGERIE DE LA GARE"
        };

        // Act
        var matches = await _recurringService.GetMatchesForTransactionsAsync(new[] { tx });

        // Assert
        matches.Should().ContainKey(tx.Id);
        var match = matches[tx.Id];
        match.MatchCount.Should().Be(0);
        match.FirstExpenseName.Should().BeNull();
        match.MatchingExpenseNames.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMatchesForTransactionsAsync_ShouldRespectAccountFilter_AndIgnoreInactive()
    {
        // Arrange
        var otherBank = new Bank { Id = Guid.NewGuid(), UserId = "test-user-id", Name = "Autre Banque" };
        var otherAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankId = otherBank.Id,
            Name = "Autre Compte"
        };
        _context.Banks.Add(otherBank);
        _context.BankAccounts.Add(otherAccount);
        await _context.SaveChangesAsync();

        var expAccountRestricted = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "EDF Autre Compte",
            Pattern = "EDF",
            TargetBankAccountId = otherAccount.Id,
            IsActive = true
        };
        var expInactive = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "EDF Inactif",
            Pattern = "EDF",
            IsActive = false
        };
        _context.RecurringExpenses.AddRange(expAccountRestricted, expInactive);
        await _context.SaveChangesAsync();

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 85m,
            Label = "PRLV EDF ENERGIE"
        };

        // Act
        var matches = await _recurringService.GetMatchesForTransactionsAsync(new[] { tx });

        // Assert
        matches.Should().ContainKey(tx.Id);
        matches[tx.Id].MatchCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMatchesForTransactionsAsync_ShouldPopulateExpenseIds_WhenMatched()
    {
        // Arrange
        var expenseId = Guid.NewGuid();
        var expense = new RecurringExpense
        {
            Id = expenseId,
            UserId = "test-user-id",
            Name = "Spotify Premium",
            Pattern = "SPOTIFY",
            IsActive = true
        };
        _context.RecurringExpenses.Add(expense);
        await _context.SaveChangesAsync();

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 10.99m,
            Label = "SPOTIFY ABONNEMENT"
        };

        // Act
        var matches = await _recurringService.GetMatchesForTransactionsAsync(new[] { tx });

        // Assert
        matches.Should().ContainKey(tx.Id);
        var match = matches[tx.Id];
        match.MatchCount.Should().Be(1);
        match.FirstExpenseId.Should().Be(expenseId);
        match.MatchingExpenseIds.Should().Contain(expenseId);
    }

    [Fact]
    public async Task GetMatchesForTransactionsAsync_WhenPatternMatchesButAmountDiffers_ShouldNotMatch()
    {
        // Arrange
        var expense1 = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Argent de poche",
            Pattern = "PIXPAY PARIS",
            ExpectedAmount = 25m,
            AmountTolerancePercentage = 0.05m,
            IsActive = true
        };
        var expense2 = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Abonnement Pixpay",
            Pattern = "X0305 PIXPAY",
            ExpectedAmount = 5.99m,
            AmountTolerancePercentage = 0.05m,
            IsActive = true
        };
        _context.RecurringExpenses.AddRange(expense1, expense2);
        await _context.SaveChangesAsync();

        var tx20Euros = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 20.00m,
            Label = "Pixpay Paris Argent de poche - PAIEMENT PAR CARTE X0305 Pixpay Paris 08/10"
        };

        var tx25Euros = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = DateTime.Today,
            Amount = 25.00m,
            Label = "Pixpay Paris Argent de poche - PAIEMENT PAR CARTE X0305 Pixpay Paris 08/10"
        };

        // Act
        var matches = await _recurringService.GetMatchesForTransactionsAsync(new[] { tx20Euros, tx25Euros });

        // Assert
        matches[tx20Euros.Id].MatchCount.Should().Be(0); // 20€ does not match 25€ nor 5.99€
        matches[tx25Euros.Id].MatchCount.Should().Be(1); // 25€ matches 25€
        matches[tx25Euros.Id].FirstExpenseId.Should().Be(expense1.Id);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_WithVariousFrequenciesAndStatuses_AggregatesAccurately()
    {
        // 1. Catégorie Salaire
        var salaryCat = new Category
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Salaire Ingénieur",
            IsIncome = true,
            IsSalary = true
        };
        _context.Categories.Add(salaryCat);

        // 2. Charges récurrentes avec différentes fréquences
        var expMonthly = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Abonnement Internet",
            Pattern = "FREEBOX*",
            ExpectedAmount = 39.99m,
            ExpectedDayOfMonth = 5,
            Frequency = RecurringFrequency.Monthly,
            IsActive = true
        };

        var expQuarterly = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Eau Régie",
            Pattern = "REGIE EAU*",
            ExpectedAmount = 120m,
            ExpectedDayOfMonth = 15,
            Frequency = RecurringFrequency.Quarterly, // Mois 1, 3, 4, 6, 7, 9, 10, 12
            IsActive = true
        };

        var expBiannual = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Assurance Auto",
            Pattern = "PACIFICA AUTO*",
            ExpectedAmount = 250m,
            ExpectedDayOfMonth = 20,
            Frequency = RecurringFrequency.Biannual, // Mois 1, 6, 7, 12
            IsActive = true
        };

        var expAnnual = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Taxe Foncière",
            Pattern = "DGFIP TAXE*",
            ExpectedAmount = 1200m,
            ExpectedDayOfMonth = 15,
            Frequency = RecurringFrequency.Annual,
            IsActive = true
        };

        _context.RecurringExpenses.AddRange(expMonthly, expQuarterly, expBiannual, expAnnual);

        // 3. Transactions du mois de janvier 2026
        var txSalary = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc),
            Amount = 3200m,
            Type = TransactionType.Credit,
            Label = "VIR REMUNERATION SOCIETE",
            CategoryId = salaryCat.Id
        };

        var txInternet = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            BankAccountId = _bankAccountId,
            Date = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            Amount = 39.99m,
            Type = TransactionType.Debit,
            Label = "FREEBOX SAS PRLV"
        };

        _context.Transactions.AddRange(txSalary, txInternet);
        await _context.SaveChangesAsync();

        // Act 1 : Mois de Janvier 2026 (Mois passé)
        var summaryJan = await _recurringService.GetMonthlySummaryAsync(2026, 1);

        // Assert Janvier
        summaryJan.TotalIncome.Should().Be(3200m);
        summaryJan.SalaryIncome.Should().Be(3200m);
        summaryJan.TotalDebited.Should().Be(39.99m);

        // Monthly internet is debited
        var itemInternet = summaryJan.Items.First(i => i.Expense.Id == expMonthly.Id);
        itemInternet.Status.Should().Be(RecurringMonthStatus.Debited);
        itemInternet.ActualDebitedAmount.Should().Be(39.99m);

        // Quarterly eau was not debited in past month -> Overdue
        var itemEau = summaryJan.Items.First(i => i.Expense.Id == expQuarterly.Id);
        itemEau.Status.Should().Be(RecurringMonthStatus.Overdue);

        // Act 2 : Mois de Février 2026 (Mois 2)
        // Mois 2 : Quarterly (3,6,9,12 ou 1,4,7,10) et Biannual (1,6,7,12) ne s'appliquent pas
        var summaryFeb = await _recurringService.GetMonthlySummaryAsync(2026, 2);
        summaryFeb.Items.Any(i => i.Expense.Id == expQuarterly.Id).Should().BeFalse();
        summaryFeb.Items.Any(i => i.Expense.Id == expBiannual.Id).Should().BeFalse();
        summaryFeb.Items.Any(i => i.Expense.Id == expMonthly.Id).Should().BeTrue();
    }

    [Fact]
    public async Task RecurringExpense_CrudEdgeCasesAndIgnoredPatterns_CoverAllBranches()
    {
        // 1. Update inexistant
        var nonExistent = new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Inexistant",
            Pattern = "TEST",
            ExpectedAmount = 10m
        };
        var updateResult = await _recurringService.UpdateAsync(nonExistent);
        updateResult.Should().BeNull();

        // 2. Delete inexistant
        var deleteResult = await _recurringService.DeleteAsync(Guid.NewGuid());
        deleteResult.Should().BeFalse();

        // 3. Patterns ignorés au départ : vide
        var initialIgnored = await _recurringService.GetIgnoredPatternsAsync();
        initialIgnored.Should().BeEmpty();

        // 4. Ignorer un pattern vide ou whitespace (ne fait rien)
        await _recurringService.IgnoreCandidateAsync("");
        await _recurringService.IgnoreCandidateAsync("   ");
        (await _recurringService.GetIgnoredPatternsAsync()).Should().BeEmpty();

        // 5. Ignorer un pattern valide
        await _recurringService.IgnoreCandidateAsync("PATRON_TEST");
        var ignoredAfterAdd = await _recurringService.GetIgnoredPatternsAsync();
        ignoredAfterAdd.Should().ContainSingle().Which.Should().Be("PATRON_TEST");

        // 6. Ignorer le même pattern une 2ème fois (pas de doublon)
        await _recurringService.IgnoreCandidateAsync("PATRON_TEST");
        (await _recurringService.GetIgnoredPatternsAsync()).Should().HaveCount(1);

        // 7. Désignorer un pattern inexistant (ne lève pas d'exception)
        await _recurringService.UnignoreCandidateAsync("PATRON_INEXISTANT");
        (await _recurringService.GetIgnoredPatternsAsync()).Should().HaveCount(1);

        // 8. Désignorer le pattern ignoré
        await _recurringService.UnignoreCandidateAsync("PATRON_TEST");
        (await _recurringService.GetIgnoredPatternsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DetectCandidatesAsync_WhenTodayIsFixed_IgnoresTransactionsOlderThanTwelveMonths()
    {
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));
        var service = new RecurringExpenseService(_context, fakeTime);

        var datesInWindow = new[]
        {
            new DateTime(2024, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 9, 10, 0, 0, 0, DateTimeKind.Utc)
        };
        foreach (var date in datesInWindow)
        {
            _context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = "test-user-id",
                BankAccountId = _bankAccountId,
                Date = date,
                Amount = 29.99m,
                Type = TransactionType.Debit,
                Label = "ABONNEMENT FIBRE 2024"
            });
        }

        await _context.SaveChangesAsync();

        var candidates = await service.DetectCandidatesAsync();

        candidates.Should().ContainSingle(c => c.Pattern.Contains("ABONNEMENT FIBRE", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly DateTimeOffset DetectionToday = new(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);

    private async Task<List<RecurringCandidateDto>> DetectWithDebitsAsync(string label, params (DateTime Date, decimal Amount)[] debits)
    {
        foreach (var (date, amount) in debits)
        {
            _context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = "test-user-id",
                BankAccountId = _bankAccountId,
                Date = date,
                Amount = amount,
                Type = TransactionType.Debit,
                Label = label
            });
        }

        await _context.SaveChangesAsync();
        var service = new RecurringExpenseService(_context, new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DetectionToday));
        var candidates = await service.DetectCandidatesAsync();
        return candidates.Where(c => c.Pattern.Contains(label.Split(' ')[^1], StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DetectCandidatesAsync_WhenThreeFixedQuarterlyDebits_ReturnsQuarterlyCandidateWithScore85()
    {
        var candidates = await DetectWithDebitsAsync("ASSURANCE HABITATION",
            (Utc(2025, 4, 5), 120m), (Utc(2025, 7, 4), 120m), (Utc(2025, 10, 3), 120m));

        var candidate = candidates.Should().ContainSingle().Subject;
        candidate.Frequency.Should().Be(RecurringFrequency.Quarterly);
        candidate.OccurrencesCount.Should().Be(3);
        candidate.ConfidenceScore.Should().Be(85);
        candidate.ExpectedDayOfMonth.Should().Be(4);
        candidate.LastAmount.Should().Be(120m);
        candidate.SampleTransactions.Should().HaveCount(3);
    }

    [Fact]
    public async Task DetectCandidatesAsync_WhenTwoDebitsOneYearApart_ReturnsAnnualCandidateWithScore65()
    {
        var candidates = await DetectWithDebitsAsync("COTISATION ANNUELLE",
            (Utc(2025, 3, 12), 80m), (Utc(2026, 3, 12), 80m));

        var candidate = candidates.Should().ContainSingle().Subject;
        candidate.Frequency.Should().Be(RecurringFrequency.Annual);
        candidate.ConfidenceScore.Should().Be(65);
    }

    [Fact]
    public async Task DetectCandidatesAsync_WhenIntervalsAreIrregular_ReturnsNoCandidate()
    {
        var candidates = await DetectWithDebitsAsync("ACHAT IRREGULIER",
            (Utc(2025, 5, 1), 50m), (Utc(2025, 6, 20), 50m), (Utc(2025, 8, 9), 50m));

        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectCandidatesAsync_WhenAmountsVaryByMoreThanTwoAndAHalf_ReturnsNoCandidate()
    {
        var candidates = await DetectWithDebitsAsync("FACTURE VARIABLE",
            (Utc(2025, 5, 3), 10m), (Utc(2025, 6, 3), 20m), (Utc(2025, 7, 3), 30m));

        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectCandidatesAsync_WhenIssuerHasTwoStableAmounts_SplitsIntoOneCandidatePerAmount()
    {
        var candidates = await DetectWithDebitsAsync("CONTRAT MUTUELLE",
            (Utc(2025, 4, 2), 45m), (Utc(2025, 5, 2), 45m), (Utc(2025, 6, 2), 45m),
            (Utc(2025, 4, 12), 25m), (Utc(2025, 5, 12), 25m), (Utc(2025, 6, 12), 25m));

        candidates.Select(c => c.AverageAmount).Should().BeEquivalentTo([45m, 25m]);
        candidates.Should().OnlyContain(c => c.Frequency == RecurringFrequency.Monthly && c.OccurrencesCount == 3);
    }

    [Fact]
    public async Task DetectCandidatesAsync_WhenRecurringExpenseAlreadyMatchesPattern_FlagsCandidateAsConfigured()
    {
        _context.RecurringExpenses.Add(new RecurringExpense
        {
            Id = Guid.NewGuid(),
            UserId = "test-user-id",
            Name = "Salle de sport",
            Pattern = "SALLE",
            ExpectedAmount = 30m,
            IsActive = true
        });

        var candidates = await DetectWithDebitsAsync("ABONNEMENT SALLE",
            (Utc(2025, 5, 8), 30m), (Utc(2025, 6, 8), 30m), (Utc(2025, 7, 8), 30m));

        var candidate = candidates.Should().ContainSingle().Subject;
        candidate.IsAlreadyConfigured.Should().BeTrue();
        candidate.ConfiguredExpenseName.Should().Be("salle de sport");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
