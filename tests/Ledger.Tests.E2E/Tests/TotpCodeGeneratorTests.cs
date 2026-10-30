using AwesomeAssertions;
using Ledger.Tests.E2E.Infrastructure;

namespace Ledger.Tests.E2E.Tests;

public sealed class TotpCodeGeneratorTests
{
    // Vecteur de test de la RFC 6238 (annexe B, SHA-1, T = 59 s) : le code complet à 8 chiffres est 94287082.
    private const string Rfc6238Key = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Fact]
    public void Generate_WhenKeyIsRfc6238TestVector_ReturnsExpectedSixDigitCode()
    {
        var code = TotpCodeGenerator.Generate(Rfc6238Key, DateTimeOffset.FromUnixTimeSeconds(59));

        code.Should().Be("287082");
    }

    [Fact]
    public void Generate_WhenKeyIsGroupedBySpaces_ReturnsSameCodeAsUngroupedKey()
    {
        var instant = DateTimeOffset.FromUnixTimeSeconds(1_111_111_109);
        var grouped = "gezd gnbv gy3t qojq gezd gnbv gy3t qojq";

        TotpCodeGenerator.Generate(grouped, instant).Should().Be(TotpCodeGenerator.Generate(Rfc6238Key, instant));
    }

    [Fact]
    public void Generate_WhenKeyContainsInvalidCharacter_ThrowsFormatException()
    {
        var act = () => TotpCodeGenerator.Generate("INVALID1", DateTimeOffset.UnixEpoch);

        act.Should().Throw<FormatException>();
    }
}
