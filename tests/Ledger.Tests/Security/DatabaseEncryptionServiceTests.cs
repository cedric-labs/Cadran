using FluentAssertions;
using Ledger.Features.Security.Context;
using Ledger.Features.Security.Services;
using Ledger.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ledger.Tests.Security;

public class DatabaseEncryptionServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testKeyPath;
    private readonly string _connectionString;
    private readonly DatabaseKeyHolder _keyHolder;
    private readonly LocalProtectedFileKeyStore _keyStore;
    private readonly DatabaseConnectionProvider _connectionProvider;
    private readonly ServiceProvider _serviceProvider;
    private readonly DatabaseEncryptionService _service;

    public DatabaseEncryptionServiceTests()
    {
        SQLitePCL.Batteries_V2.Init();
        _testDbPath = Path.Combine(Path.GetTempPath(), $"ledger_enc_svc_{Guid.NewGuid():N}.db");
        _testKeyPath = Path.Combine(Path.GetTempPath(), $"ledger_enc_key_{Guid.NewGuid():N}.dat");
        _connectionString = $"Data Source={_testDbPath};Mode=ReadWriteCreate;Foreign Keys=True;";

        _keyHolder = new DatabaseKeyHolder();
        _keyStore = new LocalProtectedFileKeyStore(_testKeyPath);
        _connectionProvider = new DatabaseConnectionProvider(_connectionString, _keyHolder);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDatabaseKeyHolder>(_keyHolder);
        services.AddSingleton<IDatabaseConnectionProvider>(_connectionProvider);
        _serviceProvider = services.BuildServiceProvider();

        _service = new DatabaseEncryptionService(
            _connectionProvider,
            _keyHolder,
            _keyStore,
            _serviceProvider,
            NullLogger<DatabaseEncryptionService>.Instance);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _serviceProvider.Dispose();
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            if (File.Exists(_testKeyPath)) File.Delete(_testKeyPath);
        }
        catch
        {
            // Ignore
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void CheckIfFileEncrypted_ReturnsFalse_WhenFileDoesNotExist()
    {
        DatabaseEncryptionService.CheckIfFileEncrypted("non_existent_file.db").Should().BeFalse();
    }

    [Fact]
    public async Task ProcessEncryptionLifecycle_WhenInvoked_EncryptsRekeysAndDecrypts()
    {
        // 1. Create a plain database with initial data
        using (var conn = new SqliteConnection(_connectionString))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Users (Id INT, Name TEXT); INSERT INTO Users VALUES (1, 'Alice');";
            await cmd.ExecuteNonQueryAsync();
        }

        SqliteConnection.ClearAllPools();
        DatabaseEncryptionService.CheckIfFileEncrypted(_testDbPath).Should().BeFalse();
        _service.IsDatabaseEncrypted().Should().BeFalse();

        // 2. Encrypt database with Master Passphrase
        var pass1 = "MasterPassphrase123!";
        var (encSuccess, encError) = await _service.EncryptDatabaseAsync(pass1, rememberOnDevice: true);
        encSuccess.Should().BeTrue();
        encError.Should().BeNull();

        DatabaseEncryptionService.CheckIfFileEncrypted(_testDbPath).Should().BeTrue();
        _service.IsDatabaseEncrypted().Should().BeTrue();
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeTrue();

        // 3. Verify passphrase validation
        _service.VerifyPassphrase(pass1).Should().BeTrue();
        _service.VerifyPassphrase("WrongPassword").Should().BeFalse();

        // 4. Test rekey (change master passphrase)
        var pass2 = "UpdatedPassphrase456!";
        var (rekeySuccess, rekeyError) = await _service.ChangePassphraseAsync(pass1, pass2, rememberOnDevice: false);
        rekeyError.Should().BeNull();
        rekeySuccess.Should().BeTrue();

        _service.VerifyPassphrase(pass2).Should().BeTrue();
        _service.VerifyPassphrase(pass1).Should().BeFalse();
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeFalse();

        // 5. Simulate App Restart in Locked State
        _keyHolder.SetKey(null, isEncrypted: true);
        _service.IsDatabaseLocked().Should().BeTrue();

        // Unlock with wrong password fails
        var unlockFail = await _service.UnlockDatabaseAsync("BadPassword", rememberOnDevice: false);
        unlockFail.Should().BeFalse();
        _service.IsDatabaseLocked().Should().BeTrue();

        // Unlock with correct password succeeds
        var unlockSuccess = await _service.UnlockDatabaseAsync(pass2, rememberOnDevice: true);
        unlockSuccess.Should().BeTrue();
        _service.IsDatabaseLocked().Should().BeFalse();
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeTrue();

        // 6. Decrypt database back to plain text
        var (decSuccess, decError) = await _service.DecryptDatabaseToPlainAsync(pass2);
        decSuccess.Should().BeTrue();
        decError.Should().BeNull();

        DatabaseEncryptionService.CheckIfFileEncrypted(_testDbPath).Should().BeFalse();
        _service.IsDatabaseEncrypted().Should().BeFalse();
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeFalse();

        // 7. Data preserved throughout all transitions
        using (var conn = new SqliteConnection(_connectionString))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Name FROM Users WHERE Id = 1;";
            var name = (string?)await cmd.ExecuteScalarAsync();
            name.Should().Be("Alice");
        }
    }

    [Fact]
    public async Task EncryptDatabaseAsync_WithInvalidPassphrase_ReturnsError()
    {
        var (success1, error1) = await _service.EncryptDatabaseAsync("short", rememberOnDevice: false);
        success1.Should().BeFalse();
        error1.Should().Contain("8 caractères");

        var (success2, error2) = await _service.EncryptDatabaseAsync("", rememberOnDevice: false);
        success2.Should().BeFalse();
    }

    [Fact]
    public async Task SetRememberOnDeviceAsync_WhenInvoked_TogglesAndPersistsStatus()
    {
        _keyHolder.SetKey("ValidKey123!", isEncrypted: true);

        await _service.SetRememberOnDeviceAsync(true);
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeTrue();

        await _service.SetRememberOnDeviceAsync(false);
        (await _service.IsRememberOnDeviceEnabledAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ChangePassphraseAsync_WhenPassphraseInvalid_ValidatesErrorsCorrectly()
    {
        // 1. ChangePassphrase when database not encrypted
        var (s1, e1) = await _service.ChangePassphraseAsync("current", "short", false);
        s1.Should().BeFalse();
        e1.Should().Contain("n'est pas chiffrée");

        // Mark as encrypted
        _keyHolder.SetKey("ValidOldPassword123!", isEncrypted: true);

        // 2. ChangePassphrase with invalid current
        var (s2, e2) = await _service.ChangePassphraseAsync("WrongCurrentPassword", "ValidNewPassword123!", false);
        s2.Should().BeFalse();
        e2.Should().Contain("incorrecte");

        // 3. Decrypt with invalid current
        var (s3, e3) = await _service.DecryptDatabaseToPlainAsync("WrongPassphrase");
        s3.Should().BeFalse();
        e3.Should().Contain("incorrecte");

        // 4. Unlock with whitespace
        var unlockEmpty = await _service.UnlockDatabaseAsync("   ", false);
        unlockEmpty.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        Action a1 = () => { _ = new DatabaseEncryptionService(null!, _keyHolder, _keyStore, _serviceProvider, NullLogger<DatabaseEncryptionService>.Instance); };
        Action a2 = () => { _ = new DatabaseEncryptionService(_connectionProvider, null!, _keyStore, _serviceProvider, NullLogger<DatabaseEncryptionService>.Instance); };
        Action a3 = () => { _ = new DatabaseEncryptionService(_connectionProvider, _keyHolder, null!, _serviceProvider, NullLogger<DatabaseEncryptionService>.Instance); };
        Action a4 = () => { _ = new DatabaseEncryptionService(_connectionProvider, _keyHolder, _keyStore, null!, NullLogger<DatabaseEncryptionService>.Instance); };
        Action a5 = () => { _ = new DatabaseEncryptionService(_connectionProvider, _keyHolder, _keyStore, _serviceProvider, null!); };

        a1.Should().Throw<ArgumentNullException>();
        a2.Should().Throw<ArgumentNullException>();
        a3.Should().Throw<ArgumentNullException>();
        a4.Should().Throw<ArgumentNullException>();
        a5.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task UnlockDatabaseAsync_WithFullDbContext_SucceedsWithoutError()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ledger_unlock_full_{Guid.NewGuid():N}.db");
        var keyPath = Path.Combine(Path.GetTempPath(), $"ledger_unlock_key_{Guid.NewGuid():N}.dat");
        var connStr = $"Data Source={dbPath};Mode=ReadWriteCreate;Foreign Keys=True;";

        var keyHolder = new DatabaseKeyHolder();
        var keyStore = new LocalProtectedFileKeyStore(keyPath);
        var connectionProvider = new DatabaseConnectionProvider(connStr, keyHolder);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDatabaseKeyHolder>(keyHolder);
        services.AddSingleton<IDatabaseConnectionProvider>(connectionProvider);
        services.AddSingleton<IDatabaseKeyStore>(keyStore);
        services.AddScoped<ICurrentUserService>(_ => new CurrentUserService(new Microsoft.AspNetCore.Http.HttpContextAccessor()));
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(connectionProvider.GetConnectionString());
        });
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddScoped<Ledger.Features.Settings.Services.IUiSettingsService, Ledger.Features.Settings.Services.UiSettingsService>();
        services.AddSingleton<Ledger.Infrastructure.Configuration.ICurrencyService, Ledger.Infrastructure.Configuration.CurrencyService>();

        using var sp = services.BuildServiceProvider();
        var encService = new DatabaseEncryptionService(
            connectionProvider,
            keyHolder,
            keyStore,
            sp,
            NullLogger<DatabaseEncryptionService>.Instance);

        try
        {
            // Initial create
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            // Encrypt
            var pass = "#exp_PWD08";
            var (encSuccess, _) = await encService.EncryptDatabaseAsync(pass, rememberOnDevice: false);
            encSuccess.Should().BeTrue();

            // Simulate restart in locked state
            keyHolder.SetKey(null, isEncrypted: true);
            encService.IsDatabaseLocked().Should().BeTrue();

            // While locked, UiSettingsService should return fallbacks cleanly without querying the database
            using (var scope = sp.CreateScope())
            {
                var ui = scope.ServiceProvider.GetRequiredService<Ledger.Features.Settings.Services.IUiSettingsService>();
                var font = await ui.GetActiveFontAsync();
                font.Name.Should().Be("Inter");
                var size = await ui.GetFontSizeAsync();
                size.Should().Be("14px");
            }

            // Unlock
            var unlockSuccess = await encService.UnlockDatabaseAsync(pass, rememberOnDevice: false);
            unlockSuccess.Should().BeTrue();
            encService.IsDatabaseLocked().Should().BeFalse();

            // After unlock, queries succeed
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var count = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.AppSettings);
                count.Should().BeGreaterThanOrEqualTo(0);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
            if (File.Exists(keyPath)) File.Delete(keyPath);
        }
    }
}
