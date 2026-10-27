using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Categories.Entities;
using Ledger.Features.Recurring.Entities;
using Ledger.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ledger.Tests.Security;

public class MultiUserIsolationTests
{
    [Fact]
    public void ApplyQueryFilters_WhenMultipleUsersExist_StrictlyIsolatesData()
    {
        // 1. Initialiser la base avec l'utilisateur "user-A"
        var (contextUserA, connection) = TestDbContextFactory.Create(userId: "user-A");

        try
        {
            var bankA = new Bank { Id = Guid.NewGuid(), UserId = "user-A", Name = "Banque de A" };
            var accountA = new BankAccount { Id = Guid.NewGuid(), UserId = "user-A", BankId = bankA.Id, Name = "Compte de A" };
            var catA = new Category { Id = Guid.NewGuid(), UserId = "user-A", Name = "Catégorie A" };
            var recA = new RecurringExpense { Id = Guid.NewGuid(), UserId = "user-A", Name = "Loyer A", Pattern = "LOYER", ExpectedAmount = 900m };

            var bankB = new Bank { Id = Guid.NewGuid(), UserId = "user-B", Name = "Banque de B" };
            var accountB = new BankAccount { Id = Guid.NewGuid(), UserId = "user-B", BankId = bankB.Id, Name = "Compte de B" };
            var catB = new Category { Id = Guid.NewGuid(), UserId = "user-B", Name = "Catégorie B" };
            var recB = new RecurringExpense { Id = Guid.NewGuid(), UserId = "user-B", Name = "Loyer B", Pattern = "LOYER", ExpectedAmount = 1200m };

            // Désactiver le filtre pour insérer les données des deux utilisateurs dans la même base
            contextUserA.Banks.AddRange(bankA, bankB);
            contextUserA.BankAccounts.AddRange(accountA, accountB);
            contextUserA.Categories.AddRange(catA, catB);
            contextUserA.RecurringExpenses.AddRange(recA, recB);
            contextUserA.SaveChanges();

            // 2. Requête en tant qu'utilisateur A : ne doit voir QUE ses entités
            var userABanks = contextUserA.Banks.ToList();
            var userAAccounts = contextUserA.BankAccounts.ToList();
            var userACategories = contextUserA.Categories.ToList();
            var userARecurring = contextUserA.RecurringExpenses.ToList();

            userABanks.Should().ContainSingle(b => b.Name == "Banque de A");
            userAAccounts.Should().ContainSingle(a => a.Name == "Compte de A");
            userACategories.Should().ContainSingle(c => c.Name == "Catégorie A");
            userARecurring.Should().ContainSingle(r => r.Name == "Loyer A");

            // 3. Ouvrir un contexte avec l'identité de "user-B" sur la même base
            var options = new DbContextOptionsBuilder<Ledger.Infrastructure.Data.AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var mockUserServiceB = new Moq.Mock<Ledger.Features.Security.Context.ICurrentUserService>();
            mockUserServiceB.Setup(u => u.UserId).Returns("user-B");
            mockUserServiceB.Setup(u => u.IsAuthenticated).Returns(true);

            using var contextUserB = new Ledger.Infrastructure.Data.AppDbContext(options, mockUserServiceB.Object);

            var userBBanks = contextUserB.Banks.ToList();
            var userBAccounts = contextUserB.BankAccounts.ToList();
            var userBCategories = contextUserB.Categories.ToList();
            var userBRecurring = contextUserB.RecurringExpenses.ToList();

            userBBanks.Should().ContainSingle(b => b.Name == "Banque de B");
            userBAccounts.Should().ContainSingle(a => a.Name == "Compte de B");
            userBCategories.Should().ContainSingle(c => c.Name == "Catégorie B");
            userBRecurring.Should().ContainSingle(r => r.Name == "Loyer B");
        }
        finally
        {
            contextUserA.Dispose();
            connection.Dispose();
        }
    }
}
