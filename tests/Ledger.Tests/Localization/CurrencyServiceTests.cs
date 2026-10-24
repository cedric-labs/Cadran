using System.Globalization;
using FluentAssertions;
using Ledger.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ledger.Tests.Localization;

public class CurrencyServiceTests
{
    private static readonly string[] ExpectedDefaultCodes = ["EUR", "USD", "GBP", "CHF"];
    private static readonly string[] ExpectedCustomCodes = ["EUR", "BTC"];

    [Fact]
    public void GetSupportedCurrencies_WithoutConfig_ReturnsDefaultCurrencies()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new CurrencyService(config);

        var currencies = service.GetSupportedCurrencies();

        currencies.Should().NotBeEmpty();
        currencies.Select(c => c.Code).Should().Contain(ExpectedDefaultCodes);
    }

    [Fact]
    public void GetSupportedCurrencies_WithCustomConfig_LoadsFromConfiguration()
    {
        var json = @"[
            {""Code"": ""EUR"", ""Symbol"": ""€"", ""DisplayName"": ""Euro"", ""FlagEmoji"": ""🇪🇺""},
            {""Code"": ""BTC"", ""Symbol"": ""₿"", ""DisplayName"": ""Bitcoin"", ""FlagEmoji"": ""🪙""}
        ]";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Currencies:Supported"] = json
            })
            .Build();

        var service = new CurrencyService(config);
        var currencies = service.GetSupportedCurrencies();

        currencies.Should().HaveCount(2);
        currencies.Select(c => c.Code).Should().Contain(ExpectedCustomCodes);
        service.GetCurrency("BTC").Symbol.Should().Be("₿");
    }

    [Theory]
    [InlineData("EUR", "€")]
    [InlineData("USD", "$")]
    [InlineData("GBP", "£")]
    [InlineData("CHF", "CHF")]
    [InlineData("JPY", "¥")]
    public void GetSymbol_WhenCurrencyProvided_ReturnsExpectedSymbol(string code, string expectedSymbol)
    {
        var config = new ConfigurationBuilder().Build();
        var service = new CurrencyService(config);

        service.GetSymbol(code).Should().Be(expectedSymbol);
    }

    [Fact]
    public void FormatAmount_WhenAmountProvided_FormatsCorrectlyAccordingToCurrencyAndCulture()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new CurrencyService(config);

        var fr = new CultureInfo("fr");
        var en = new CultureInfo("en");

        // Formatage EUR
        service.FormatAmount(1234.56m, "EUR", true, fr).Should().Contain("1")
            .And.Contain("234,56")
            .And.Contain("€");
        service.FormatAmount(1234.56m, "EUR", true, en).Should().Be("€1,234.56");

        // Formatage USD
        service.FormatAmount(1234.56m, "USD", true, fr).Should().Contain("1")
            .And.Contain("234,56")
            .And.Contain("$");
        service.FormatAmount(1234.56m, "USD", true, en).Should().Be("$1,234.56");

        // Formatage GBP
        service.FormatAmount(1234.56m, "GBP", true, en).Should().Be("£1,234.56");

        // Formatage sans centimes
        service.FormatAmount(1234.56m, "USD", false, en).Should().Be("$1,235");
    }

    [Fact]
    public void ToCurrency_ExtensionMethod_FormatsWithConfiguredService()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new CurrencyService(config);
        CurrencyExtensions.Configure(service);

        var en = new CultureInfo("en");

        100m.ToCurrency("USD", true, en).Should().Be("$100.00");
        100m.ToCurrency("EUR", true, en).Should().Be("€100.00");
        100m.ToCurrency("GBP", true, en).Should().Be("£100.00");
        100m.ToCurrency("CHF", true, en).Should().Be("CHF100.00");

        // Nullable
        decimal? nullVal = null;
        nullVal.ToCurrency("USD").Should().BeEmpty();

        decimal? nonNullVal = 50m;
        nonNullVal.ToCurrency("USD", true, en).Should().Be("$50.00");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCurrency_WhenNullOrWhitespace_ReturnsEur(string? code)
    {
        var config = new ConfigurationBuilder().Build();
        var service = new CurrencyService(config);

        var currency = service.GetCurrency(code);
        currency.Code.Should().Be("EUR");
        currency.Symbol.Should().Be("€");
    }

    [Fact]
    public void GetCurrency_WhenUnknownCode_ReturnsFallbackCurrencyInfo()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new CurrencyService(config);

        var currency = service.GetCurrency("unknown_xyz");
        currency.Code.Should().Be("UNKNOWN_XYZ");
        currency.Symbol.Should().Be("UNKNOWN_XYZ");
        currency.DisplayName.Should().Be("UNKNOWN_XYZ");
    }

    [Fact]
    public void LoadCurrencies_WhenJsonIsInvalidOrEmptyList_FallsBackGracefully()
    {
        // 1. JSON invalide
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Currencies:Supported"] = "{ this is not valid json }"
            })
            .Build();
        var service1 = new CurrencyService(invalidConfig);
        service1.GetSupportedCurrencies().Should().NotBeEmpty();

        // 2. JSON liste vide
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Currencies:Supported"] = "[]"
            })
            .Build();
        var service2 = new CurrencyService(emptyConfig);
        service2.GetSupportedCurrencies().Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(null, "€")]
    [InlineData("", "€")]
    [InlineData("   ", "€")]
    [InlineData("EUR", "€")]
    [InlineData("USD", "$")]
    [InlineData("CAD", "$ CA")]
    [InlineData("UNKNOWN_ABC", "UNKNOWN_ABC")]
    public void GetStaticSymbol_WhenCurrencyProvided_ReturnsExpectedSymbol(string? code, string expected)
    {
        CurrencyService.GetStaticSymbol(code).Should().Be(expected);
    }

    [Fact]
    public void ToCurrency_WhenServiceIsNull_UsesStaticFallback()
    {
        // Réinitialiser le service statique à null
        CurrencyExtensions.Configure(null!);

        var en = new CultureInfo("en-US");
        var result = 150.50m.ToCurrency("EUR", true, en);
        result.Should().Contain("150.50");
        result.Should().Contain("€");

        var resultNoCents = 150.50m.ToCurrency("USD", false, en);
        resultNoCents.Should().Contain("151");
        resultNoCents.Should().Contain("$");
    }

    [Fact]
    public void FormatAmount_NegativeAmountWithEnUsCulture_FormatsWithMinusSign()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new CurrencyService(config);

        var us = new CultureInfo("en-US");
        // en-US a par défaut CurrencyNegativePattern == 0 (ex: ($100.00))
        var formatted = service.FormatAmount(-100m, "USD", true, us);
        formatted.Should().Be("-$100.00");
    }
}
