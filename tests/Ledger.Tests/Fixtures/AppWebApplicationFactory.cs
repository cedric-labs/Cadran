using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Ledger.Tests.Fixtures;

public class AppWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;

    public AppWebApplicationFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ledger_prog_test_{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("Server__AutoLaunchBrowser", "false");
        Environment.SetEnvironmentVariable("Server__Port", "");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $"Data Source={_dbPath};Mode=ReadWriteCreate;Foreign Keys=True;");
    }

    public string DatabasePath => _dbPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:AutoLaunchBrowser"] = "false",
                ["Server:Port"] = null,
                ["Server:AllowNetworkAccess"] = "false",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath};Mode=ReadWriteCreate;Foreign Keys=True;"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        try
        {
            if (File.Exists(_dbPath))
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Fichier temporaire libéré par le système d'exploitation
        }
    }
}
