using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ledger.Tests.E2E.Infrastructure;

/// <summary>
/// Fixture de test initialisant une instance réelle Kestrel sur un port dynamique pour l'exécution des tests Playwright.
/// </summary>
public sealed class AppWebTestFixture : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"cadran_e2e_{Guid.NewGuid():N}.db");
    private IHost? _kestrelHost;
    private string _serverAddress = string.Empty;

    /// <summary>
    /// Adresse HTTP complète du serveur Kestrel en cours d'exécution.
    /// </summary>
    public string ServerAddress
    {
        get
        {
            EnsureServerStarted();
            return _serverAddress;
        }
    }

    /// <summary>
    /// Chemin du fichier de base de données temporaire.
    /// </summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// Fournisseur de services de l'instance réelle Kestrel en cours d'exécution.
    /// </summary>
    public IServiceProvider KestrelServices => _kestrelHost?.Services ?? Services;

    /// <summary>
    /// Force le démarrage du serveur Kestrel en initialisant l'hôte WebApplicationFactory.
    /// </summary>
    public void EnsureServerStarted()
    {
        if (!string.IsNullOrEmpty(_serverAddress))
        {
            return;
        }

        _ = Services;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        builder.UseStaticWebAssets();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Server:AutoLaunchBrowser"] = "false",
                ["Server:Port"] = null,
                ["Server:AllowNetworkAccess"] = "false",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath};Mode=ReadWriteCreate;Foreign Keys=True;"
            });
        });
    }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // 1. Construit l'hôte TestServer requis par WebApplicationFactory
        var testHost = builder.Build();

        // 2. Configure et démarre l'hôte réel Kestrel écoutant sur un port dynamique
        builder.ConfigureWebHost(webHostBuilder =>
        {
            webHostBuilder.UseKestrel(options =>
            {
                options.Listen(IPAddress.Loopback, 0);
            });
            webHostBuilder.UseStaticWebAssets();
        });

        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var server = _kestrelHost.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        var serverAddress = addresses?.Addresses.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Impossible de déterminer l'adresse HTTP dynamique de Kestrel.");

        _serverAddress = serverAddress;
        ClientOptions.BaseAddress = new Uri(serverAddress);

        return testHost;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            if (_kestrelHost is not null)
            {
                _kestrelHost.StopAsync().GetAwaiter().GetResult();
                _kestrelHost.Dispose();
                _kestrelHost = null;
            }

            try
            {
                if (File.Exists(_databasePath))
                {
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    File.Delete(_databasePath);
                }
            }
            catch (IOException)
            {
                // Toléré : fichier temporaire libéré par le système d'exploitation
            }
        }
    }
}
