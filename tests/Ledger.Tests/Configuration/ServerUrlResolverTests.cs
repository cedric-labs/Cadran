using AwesomeAssertions;
using Ledger.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ledger.Tests.Configuration;

public sealed class ServerUrlResolverTests
{
    private static IConfiguration Build(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value, StringComparer.Ordinal))
            .Build();

    [Fact]
    public void Resolve_WhenPortIsConfiguredWithNetworkAccess_ReturnsAllInterfacesUrl()
    {
        var configuration = Build(("Server:Port", "5000"), ("Server:AllowNetworkAccess", "true"));

        ServerUrlResolver.Resolve(configuration).Should().Be("http://0.0.0.0:5000");
    }

    [Fact]
    public void Resolve_WhenPortIsConfiguredWithoutNetworkAccess_ReturnsLocalhostUrl()
    {
        var configuration = Build(("Server:Port", "5000"));

        ServerUrlResolver.Resolve(configuration).Should().Be("http://localhost:5000");
    }

    [Fact]
    public void Resolve_WhenSchemeIsConfigured_UsesIt()
    {
        var configuration = Build(("Server:Port", "5001"), ("Server:Scheme", "https"));

        ServerUrlResolver.Resolve(configuration).Should().Be("https://localhost:5001");
    }

    [Fact]
    public void Resolve_WhenNoPortIsConfigured_ReturnsNull()
    {
        ServerUrlResolver.Resolve(Build()).Should().BeNull();
    }

    [Fact]
    public void Resolve_WhenUrlsComeFromCommandLineOrEnvironment_ReturnsNullSoTheyWin()
    {
        var configuration = Build(("Server:Port", "5000"), ("urls", "http://0.0.0.0:5005"));

        ServerUrlResolver.Resolve(configuration).Should().BeNull();
    }
}
