using System.Text;
using FluentAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Imports.Entities;
using Ledger.Features.Imports.Strategies;
using Xunit;

namespace Ledger.Tests.Imports;

public class CsvStatementParserTests
{
    private readonly CsvStatementParser _parser = new();

    [Fact]
    public async Task ParseAsync_StandardSemicolonCsv_ShouldParseTransactionsAccurately()
    {
        // Arrange
        var csvContent = @"Date;Libelle;Montant
15/01/2026;CARREFOUR MARKET;-45,50
28/01/2026;VIREMENT SALAIRE;2500,00
30/01/2026;PRLV SEPA NETFLIX;-17,99";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        var profile = new CsvMappingProfile
        {
            UserId = "user-1",
            Name = "Format Français",
            Delimiter = ";",
            DecimalSeparator = ",",
            DateFormat = "dd/MM/yyyy",
            HasHeader = true,
            DateColumnIndex = 0,
            LabelColumnIndex = 1,
            AmountColumnIndex = 2
        };

        // Act
        var result = await _parser.ParseAsync(stream, profile);

        // Assert
        result.Should().HaveCount(3);

        // 1ère opération : Débit 45.50
        result[0].Date.Should().Be(new DateTime(2026, 1, 15));
        result[0].Label.Should().Be("CARREFOUR MARKET");
        result[0].Amount.Should().Be(45.50m);
        result[0].Type.Should().Be(TransactionType.Debit);

        // 2ème opération : Crédit 2500.00
        result[1].Date.Should().Be(new DateTime(2026, 1, 28));
        result[1].Label.Should().Be("VIREMENT SALAIRE");
        result[1].Amount.Should().Be(2500.00m);
        result[1].Type.Should().Be(TransactionType.Credit);

        // 3ème opération : Débit 17.99
        result[2].Date.Should().Be(new DateTime(2026, 1, 30));
        result[2].Label.Should().Be("PRLV SEPA NETFLIX");
        result[2].Amount.Should().Be(17.99m);
        result[2].Type.Should().Be(TransactionType.Debit);
    }

    [Fact]
    public async Task ParseAsync_CommaCsvWithDotDecimal_ShouldParseAccurately()
    {
        // Arrange
        var csvContent = @"Date,Description,Amount
2026-02-01,AMAZON EU,-29.90
2026-02-02,REFUND AMAZON,15.00";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        var profile = new CsvMappingProfile
        {
            UserId = "user-1",
            Name = "International",
            Delimiter = ",",
            DecimalSeparator = ".",
            DateFormat = "yyyy-MM-dd",
            HasHeader = true,
            DateColumnIndex = 0,
            LabelColumnIndex = 1,
            AmountColumnIndex = 2
        };

        // Act
        var result = await _parser.ParseAsync(stream, profile);

        // Assert
        result.Should().HaveCount(2);
        result[0].Amount.Should().Be(29.90m);
        result[0].Type.Should().Be(TransactionType.Debit);
        result[1].Amount.Should().Be(15.00m);
        result[1].Type.Should().Be(TransactionType.Credit);
    }

    [Theory]
    [InlineData("releve.csv", true)]
    [InlineData("releve.CSV", true)]
    [InlineData("export.tsv", true)]
    [InlineData("export.txt", true)]
    [InlineData("document.pdf", false)]
    [InlineData("releve.ofx", false)]
    public void CanHandle_WhenExtensionChecked_ReturnsAccurateResult(string fileName, bool expected)
    {
        using var stream = new MemoryStream();
        _parser.CanHandle(fileName, stream).Should().Be(expected);
    }

    [Fact]
    public async Task ParseAsync_WithSeparateDebitAndCreditColumns_ParsesProperly()
    {
        var csvContent = @"Date;Libellé;Débit;Crédit;Référence
10/03/2026;Loyer;-800,00;;REF-123
12/03/2026;Prime;;500,00;REF-456
15/03/2026;Ligne Sans Montant;0,00;0,00;
PAS_UNE_DATE;Date Invalide;10,00;;
19/03/2026;Ligne Courte
";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var profile = new CsvMappingProfile
        {
            UserId = "user-1",
            Name = "Format Multi Colonnes",
            Delimiter = ";",
            DecimalSeparator = ",",
            DateFormat = "dd/MM/yyyy",
            HasHeader = true,
            DateColumnIndex = 0,
            LabelColumnIndex = 1,
            AmountColumnIndex = null,
            DebitColumnIndex = 2,
            CreditColumnIndex = 3,
            ReferenceColumnIndex = 4
        };

        var result = await _parser.ParseAsync(stream, profile);

        result.Should().HaveCount(2);

        result[0].Label.Should().Be("Loyer");
        result[0].Amount.Should().Be(800m);
        result[0].Type.Should().Be(TransactionType.Debit);
        result[0].Reference.Should().Be("REF-123");

        result[1].Label.Should().Be("Prime");
        result[1].Amount.Should().Be(500m);
        result[1].Type.Should().Be(TransactionType.Credit);
        result[1].Reference.Should().Be("REF-456");
    }

    [Fact]
    public async Task ParseAsync_WithNullProfileAndSpecialFormatting_ParsesWithDefaults()
    {
        // Null profile utilise le profil par défaut (dd/MM/yyyy, séparateur ;, montant colonne 2)
        // Libellé avec tabulations et espaces multiples, montant avec symbole devise et espaces insécables
        var csvContent = "Date;Libellé;Montant\n" +
                         "05/04/2026;\"Supermarché\t\tGrand   Frais\";-125,50 €\n" +
                         "\n" +
                         "06/04/2026;Virement   Client;500.00 EUR\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        var result = await _parser.ParseAsync(stream, null);

        result.Should().HaveCount(2);
        result[0].Label.Should().Be("Supermarché Grand Frais");
        result[0].Amount.Should().Be(125.50m);
        result[0].Type.Should().Be(TransactionType.Debit);

        result[1].Label.Should().Be("Virement Client");
        result[1].Amount.Should().Be(500.00m);
        result[1].Type.Should().Be(TransactionType.Credit);
    }
}
