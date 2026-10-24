using System.Text;
using FluentAssertions;
using Ledger.Features.Accounts.Entities;
using Ledger.Features.Imports.Strategies;
using Xunit;

namespace Ledger.Tests.Imports;

public class OfxStatementParserTests
{
    private readonly OfxStatementParser _parser = new();

    [Theory]
    [InlineData("statement.ofx", true)]
    [InlineData("statement.OFX", true)]
    [InlineData("bank.qfx", true)]
    [InlineData("data.csv", false)]
    [InlineData("report.pdf", false)]
    public void CanHandle_ByFileExtension_ShouldReturnExpectedResult(string fileName, bool expected)
    {
        using var stream = new MemoryStream();
        var result = _parser.CanHandle(fileName, stream);
        result.Should().Be(expected);
    }

    [Fact]
    public void CanHandle_ByContentHeader_ShouldDetectOfx()
    {
        var content = "OFXHEADER:100\r\nDATA:OFXSGML\r\n<OFX>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = _parser.CanHandle("unknown.txt", stream);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractOfxAccountIdAsync_WithValidTag_ShouldReturnAccountId()
    {
        var ofxContent = @"
OFXHEADER:100
<OFX>
<BANKMSGSRSV1>
<STMTTRNRS>
<STMTRS>
<BANKACCTFROM>
<BANKID>30004
<BRANCHID>02837
<ACCTID> 00012345678A </ACCTID>
<ACCTTYPE>CHECKING
</BANKACCTFROM>
";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ofxContent));

        var accountId = await OfxStatementParser.ExtractOfxAccountIdAsync(stream);

        accountId.Should().Be("00012345678A");
    }

    [Fact]
    public async Task ExtractOfxAccountIdAsync_WithoutAcctId_ShouldReturnNull()
    {
        var ofxContent = "<OFX><BANKMSGSRSV1></BANKMSGSRSV1></OFX>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ofxContent));

        var accountId = await OfxStatementParser.ExtractOfxAccountIdAsync(stream);

        accountId.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_StandardOfx_ShouldParseTransactionsAccurately()
    {
        var ofxContent = @"
OFXHEADER:100
DATA:OFXSGML
VERSION:102
<OFX>
<BANKMSGSRSV1>
<STMTTRNRS>
<STMTRS>
<BANKTRANLIST>
<DTSTART>20260101
<DTEND>20260131
<STMTTRN>
<TRNTYPE>DEBIT
<DTPOSTED>20260115120000[0:GMT]
<TRNAMT>-45.50
<FITID>TX1001
<NAME>CARREFOUR MARKET
<MEMO>Alimentation - Courses hebdo
</STMTTRN>
<STMTTRN>
<TRNTYPE>CREDIT
<DTPOSTED>20260128
<TRNAMT>2500.00
<FITID>TX1002
<NAME>VIREMENT SALAIRE
<MEMO>Employeur
</STMTTRN>
<STMTTRN>
<TRNTYPE>DEBIT
<DTPOSTED>20260130
<TRNAMT>-17.99
<FITID>TX1003
<MEMO>Abonnement Netflix
</STMTTRN>
<STMTTRN>
<TRNTYPE>OTHER
<DTPOSTED>
<TRNAMT>10.00
</STMTTRN>
</BANKTRANLIST>
</STMTRS>
</STMTTRNRS>
</BANKMSGSRSV1>
</OFX>";

        using var stream = new MemoryStream(Encoding.Latin1.GetBytes(ofxContent));

        var result = await _parser.ParseAsync(stream, null);

        result.Should().HaveCount(3);

        // Transaction 1 : Débit
        result[0].Date.Should().Be(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        result[0].Amount.Should().Be(45.50m);
        result[0].Type.Should().Be(TransactionType.Debit);
        result[0].Label.Should().Be("CARREFOUR MARKET");
        result[0].CategoryName.Should().Be("Alimentation");
        result[0].Reference.Should().Be("TX1001");
        result[0].ExternalId.Should().Be("TX1001");

        // Transaction 2 : Crédit
        result[1].Date.Should().Be(new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc));
        result[1].Amount.Should().Be(2500.00m);
        result[1].Type.Should().Be(TransactionType.Credit);
        result[1].Label.Should().Be("VIREMENT SALAIRE");
        result[1].CategoryName.Should().BeNull();

        // Transaction 3 : Libellé depuis MEMO quand NAME est absent
        result[2].Date.Should().Be(new DateTime(2026, 1, 30, 0, 0, 0, DateTimeKind.Utc));
        result[2].Amount.Should().Be(17.99m);
        result[2].Type.Should().Be(TransactionType.Debit);
        result[2].Label.Should().Be("Abonnement Netflix");
    }

    [Fact]
    public async Task ParseAsync_WhenNameAndMemoEmpty_ShouldFallbackDefaultLabel()
    {
        var ofxContent = @"
<OFX>
<STMTTRN>
<DTPOSTED>20260201
<TRNAMT>-10.50
<FITID>TX9999
</STMTTRN>
</OFX>";

        using var stream = new MemoryStream(Encoding.Latin1.GetBytes(ofxContent));

        var result = await _parser.ParseAsync(stream, null);

        result.Should().HaveCount(1);
        result[0].Label.Should().Be("Opération OFX");
        result[0].Amount.Should().Be(10.50m);
    }
}
