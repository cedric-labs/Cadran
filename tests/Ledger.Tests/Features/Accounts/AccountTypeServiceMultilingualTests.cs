using System.Globalization;
using AwesomeAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Accounts.Pages;
using Ledger.Features.Accounts.Services;
using Ledger.Features.Security.Context;
using Ledger.Infrastructure.Configuration;
using Ledger.Infrastructure.Data;
using Ledger.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ledger.Tests.Features.Accounts;

public sealed class AccountTypeServiceMultilingualTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly AccountTypeService _service;
    private readonly string _userId = "user-multi-lang";

    public AccountTypeServiceMultilingualTests()
    {
        (_context, _connection) = TestDbContextFactory.Create(_userId);
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(u => u.UserId).Returns(_userId);

        _service = new AccountTypeService(_context, mockUserService.Object, NullLogger<AccountTypeService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task EnsureDefaultAccountTypesForUserAsync_WhenInvoked_SeedsDefaultTypesAndTranslationsInEnEsDe()
    {
        // Act
        await _service.EnsureDefaultAccountTypesForUserAsync(_userId);

        // Assert
        var types = await _context.CustomAccountTypes.Where(t => t.UserId == _userId).ToListAsync();
        types.Should().HaveCount(17);

        var translations = await _context.EntityTranslations
            .Where(t => t.EntityType == "CustomAccountType" && t.Field == "Name")
            .ToListAsync();

        // 17 types x 3 langues (en, es, de) = 51 traductions
        translations.Should().HaveCount(51);

        var checkingType = types.First(t => t.Code == "Checking");
        var checkingTrans = translations.Where(t => t.EntityId == checkingType.Id).ToDictionary(t => t.LanguageCode, t => t.Translation);

        checkingTrans["en"].Should().Be("Checking Account");
        checkingTrans["es"].Should().Be("Cuenta Corriente");
        checkingTrans["de"].Should().Be("Girokonto");
    }

    [Fact]
    public async Task GetAllAccountTypesAsync_WhenTypesExistWithoutTranslations_SeedsMissingTranslationsIdempotently()
    {
        // Arrange : insérer un type natif sans traductions
        var type = new CustomAccountType
        {
            UserId = _userId,
            Name = "Livret A",
            Code = "LivretA",
            CategoryGroup = AccountCategoryGroup.Savings,
            ColorHex = "#0dcaf0",
            IconName = "bi-piggy-bank",
            IsSystem = true,
            DisplayOrder = 1
        };
        _context.CustomAccountTypes.Add(type);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllAccountTypesAsync();

        // Assert
        result.Should().NotBeEmpty();
        var translations = await _context.EntityTranslations
            .Where(t => t.EntityType == "CustomAccountType" && t.EntityId == type.Id)
            .ToDictionaryAsync(t => t.LanguageCode, t => t.Translation);

        translations.Should().ContainKey("en").WhoseValue.Should().Be("Livret A Savings");
        translations.Should().ContainKey("es").WhoseValue.Should().Be("Cuenta de ahorro Livret A");
        translations.Should().ContainKey("de").WhoseValue.Should().Be("Livret A Sparkonto");
    }

    [Fact]
    public async Task TypesModel_WhenLanguageIsSpanish_LoadsSpanishAndSavesSpanishTranslation()
    {
        // Arrange
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("es");

            var typeId = Guid.NewGuid();
            var customType = new CustomAccountType
            {
                Id = typeId,
                UserId = _userId,
                Name = "Compte de Dépôt",
                Code = "Checking",
                CategoryGroup = AccountCategoryGroup.Liquid,
                ColorHex = "#3b82f6",
                IconName = "bi-wallet2"
            };

            var mockTypeService = new Mock<IAccountTypeService>();
            mockTypeService.Setup(s => s.GetAllAccountTypesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CustomAccountType> { customType });
            mockTypeService.Setup(s => s.GetByIdAsync(typeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(customType);

            var mockLocalizer = new Mock<ILocalizedEntityService>();
            mockLocalizer.Setup(l => l.GetTranslationsAsync("CustomAccountType", It.IsAny<IEnumerable<Guid>>(), "Name", "es", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string> { [typeId] = "Cuenta Corriente" });

            var httpContext = new DefaultHttpContext();
            var pageContext = new PageContext(new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new PageActionDescriptor(), new ModelStateDictionary()));
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            var model = new TypesModel(mockTypeService.Object, _context, mockLocalizer.Object)
            {
                PageContext = pageContext,
                TempData = tempData
            };

            // Act 1 : Chargement
            await model.OnGetAsync();
            model.AccountTypes.Should().HaveCount(1);
            model.AccountTypes[0].LocalizedName.Should().Be("Cuenta Corriente");

            // Act 2 : Modification en espagnol
            model.Input = new TypesModel.TypeInputModel
            {
                Id = typeId,
                Name = "Mi Cuenta Corriente Editada",
                Code = "Checking",
                CategoryGroup = AccountCategoryGroup.Liquid,
                ColorHex = "#3b82f6",
                IconName = "bi-wallet2"
            };

            var postResult = await model.OnPostSaveAsync();

            // Assert
            postResult.Should().BeOfType<RedirectToPageResult>();
            mockLocalizer.Verify(l => l.SetTranslationAsync("CustomAccountType", typeId, "Name", "es", "Mi Cuenta Corriente Editada", It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
