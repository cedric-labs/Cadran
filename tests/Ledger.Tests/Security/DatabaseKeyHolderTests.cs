using FluentAssertions;
using Ledger.Features.Security.Context;
using Ledger.Infrastructure.Data;
using Xunit;

namespace Ledger.Tests.Security;

public class DatabaseKeyHolderTests
{
    [Fact]
    public void GetState_WhenDatabaseNotEncrypted_ReturnsUnlocked()
    {
        var holder = new DatabaseKeyHolder();

        holder.EncryptionKey.Should().BeNull();
        holder.IsDatabaseEncrypted.Should().BeFalse();
        holder.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public void KeyHolder_WhenEncryptedWithoutKey_ShouldBeLocked()
    {
        var holder = new DatabaseKeyHolder();
        holder.SetKey(null, isEncrypted: true);

        holder.IsDatabaseEncrypted.Should().BeTrue();
        holder.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public void KeyHolder_WhenEncryptedWithKey_ShouldBeUnlocked()
    {
        var holder = new DatabaseKeyHolder();
        holder.SetKey("MySecretKey123", isEncrypted: true);

        holder.EncryptionKey.Should().Be("MySecretKey123");
        holder.IsDatabaseEncrypted.Should().BeTrue();
        holder.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public void KeyHolder_Clear_ShouldResetKey()
    {
        var holder = new DatabaseKeyHolder();
        holder.SetKey("MySecretKey123", isEncrypted: true);
        holder.Clear();

        holder.EncryptionKey.Should().BeNull();
        holder.IsUnlocked.Should().BeFalse();
    }
}

public class DatabaseConnectionProviderTests
{
    [Fact]
    public void GetConnectionString_WhenNotEncrypted_ReturnsBaseConnectionString()
    {
        var baseConn = "Data Source=test.db;Mode=ReadWriteCreate;";
        var keyHolder = new DatabaseKeyHolder();
        var provider = new DatabaseConnectionProvider(baseConn, keyHolder);

        var result = provider.GetConnectionString();

        result.Should().Be(baseConn);
        provider.IsDatabaseEncrypted().Should().BeFalse();
        provider.IsDatabaseLocked().Should().BeFalse();
    }

    [Fact]
    public void GetConnectionString_WhenEncryptedWithKey_AppendsPassword()
    {
        var baseConn = "Data Source=test.db;Mode=ReadWriteCreate;";
        var keyHolder = new DatabaseKeyHolder();
        keyHolder.SetKey("SecretPassword!", isEncrypted: true);
        var provider = new DatabaseConnectionProvider(baseConn, keyHolder);

        var result = provider.GetConnectionString();

        result.Should().Contain("Password=SecretPassword!");
        provider.IsDatabaseEncrypted().Should().BeTrue();
        provider.IsDatabaseLocked().Should().BeFalse();
    }

    [Fact]
    public void IsDatabaseLocked_WhenEncryptedWithoutKey_ReturnsTrue()
    {
        var baseConn = "Data Source=test.db;Mode=ReadWriteCreate;";
        var keyHolder = new DatabaseKeyHolder();
        keyHolder.SetKey(null, isEncrypted: true);
        var provider = new DatabaseConnectionProvider(baseConn, keyHolder);

        provider.IsDatabaseLocked().Should().BeTrue();
    }

    [Fact]
    public void GetDatabaseFilePath_WhenInvoked_ResolvesCorrectPath()
    {
        var baseConn = "Data Source=custom.db;";
        var keyHolder = new DatabaseKeyHolder();
        var provider = new DatabaseConnectionProvider(baseConn, keyHolder);

        var path = provider.GetDatabaseFilePath();

        path.Should().EndWith("custom.db");
    }

    [Fact]
    public void GetDatabaseFilePath_WhenMemory_ReturnsMemory()
    {
        var baseConn = "Data Source=:memory:;";
        var keyHolder = new DatabaseKeyHolder();
        var provider = new DatabaseConnectionProvider(baseConn, keyHolder);

        var path = provider.GetDatabaseFilePath();

        path.Should().Be(":memory:");
    }
}
