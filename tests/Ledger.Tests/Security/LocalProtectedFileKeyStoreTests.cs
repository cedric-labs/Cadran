using AwesomeAssertions;
using Ledger.Features.Security.Services;
using Xunit;

namespace Ledger.Tests.Security;

public sealed class LocalProtectedFileKeyStoreTests : IDisposable
{
    private readonly string _testKeyPath;

    public LocalProtectedFileKeyStoreTests()
    {
        _testKeyPath = Path.Combine(Path.GetTempPath(), $"ledger_test_key_{Guid.NewGuid():N}.dat");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testKeyPath))
            {
                File.Delete(_testKeyPath);
            }
        }
        catch
        {
            // Ignore
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetSavedKeyAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);

        var key = await store.GetSavedKeyAsync();

        key.Should().BeNull();
        (await store.HasSavedKeyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task SaveKeyAsync_WhenCalled_SavesAndRetrievesKeyCorrectly()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);
        var expectedKey = "MySuperSecretKey#2026!";

        await store.SaveKeyAsync(expectedKey);

        (await store.HasSavedKeyAsync()).Should().BeTrue();
        var retrievedKey = await store.GetSavedKeyAsync();
        retrievedKey.Should().Be(expectedKey);
    }

    [Fact]
    public async Task ClearSavedKeyAsync_WhenKeyExists_RemovesStoredKey()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);
        await store.SaveKeyAsync("KeyToDelete");

        await store.ClearSavedKeyAsync();

        (await store.HasSavedKeyAsync()).Should().BeFalse();
        (await store.GetSavedKeyAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GetSavedKeyAsync_WhenFileCorrupted_ReturnsNullGracefully()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);
        await File.WriteAllBytesAsync(_testKeyPath, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02 });

        var key = await store.GetSavedKeyAsync();

        key.Should().BeNull();
    }

    [Fact]
    public Task SaveKeyAsync_WithEmptyKey_ThrowsArgumentException()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);

        Func<Task> act = async () => await store.SaveKeyAsync("   ");

        return act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetSavedKeyAsync_WhenFileIsEmpty_ReturnsNull()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);
        await File.WriteAllBytesAsync(_testKeyPath, Array.Empty<byte>());

        var key = await store.GetSavedKeyAsync();

        key.Should().BeNull();
    }

    [Fact]
    public void Constructor_WhenDefaultInvoked_InitializesCorrectly()
    {
        var store = new LocalProtectedFileKeyStore();
        store.Should().NotBeNull();
    }

    [Fact]
    public void SaveKey_WhenUsingSynchronousMethods_RoundTripsKey()
    {
        var store = new LocalProtectedFileKeyStore(_testKeyPath);
        store.SaveKey("SyncKeyVal");
        store.GetSavedKey().Should().Be("SyncKeyVal");
        store.ClearSavedKey();
        store.GetSavedKey().Should().BeNull();
    }

    [Fact]
    public void EncryptFallback_WhenInvoked_RoundTripsKeyCorrectly()
    {
        var encryptMethod = typeof(LocalProtectedFileKeyStore).GetMethod("EncryptFallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
        var decryptMethod = typeof(LocalProtectedFileKeyStore).GetMethod("DecryptFallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);

        encryptMethod.Should().NotBeNull();
        decryptMethod.Should().NotBeNull();

        var originalText = "CrossPlatformSecretPassphrase123!";
        var originalBytes = System.Text.Encoding.UTF8.GetBytes(originalText);

        var encryptedBytes = (byte[])encryptMethod.Invoke(null, new object[] { originalBytes })!;
        encryptedBytes.Should().NotBeNull();
        encryptedBytes.Length.Should().BeGreaterThan(originalBytes.Length);

        var decryptedBytes = (byte[])decryptMethod.Invoke(null, new object[] { encryptedBytes })!;
        var decryptedText = System.Text.Encoding.UTF8.GetString(decryptedBytes);

        decryptedText.Should().Be(originalText);
    }

    [Fact]
    public void DecryptFallback_WithTooShortData_ThrowsCryptographicException()
    {
        var decryptMethod = typeof(LocalProtectedFileKeyStore).GetMethod("DecryptFallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
        decryptMethod.Should().NotBeNull();

        var shortBytes = new byte[] { 1, 2, 3 };
        Action act = () =>
        {
            try
            {
                decryptMethod.Invoke(null, new object[] { shortBytes });
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        };

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }
}
