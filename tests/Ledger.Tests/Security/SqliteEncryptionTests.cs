using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Ledger.Tests.Security;

public class SqliteEncryptionTests : IDisposable
{
    private readonly string _testDbPath;

    public SqliteEncryptionTests()
    {
        SQLitePCL.Batteries_V2.Init();
        _testDbPath = Path.Combine(Path.GetTempPath(), $"ledger_test_enc_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void CreateAndRead_WhenPasswordProvided_SecuresDatabaseAndPermitsAuthorizedRead()
    {
        var password = "SuperSecretPassword123!";
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Password = password,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        // 1. Create and write table
        using (var connection = new SqliteConnection(builder.ConnectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE SecretData (Id INTEGER PRIMARY KEY, Val TEXT); INSERT INTO SecretData (Val) VALUES ('Hello Secure World');";
            cmd.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();

        // 2. Read with correct password
        using (var connection = new SqliteConnection(builder.ConnectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Val FROM SecretData WHERE Id = 1;";
            var val = (string?)cmd.ExecuteScalar();
            val.Should().Be("Hello Secure World");
        }

        SqliteConnection.ClearAllPools();

        // 3. Read without password must fail
        var noPassBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Mode = SqliteOpenMode.ReadOnly
        };

        using (var connection = new SqliteConnection(noPassBuilder.ConnectionString))
        {
            Action act = () =>
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM SecretData;";
                cmd.ExecuteScalar();
            };

            act.Should().Throw<SqliteException>();
        }

        SqliteConnection.ClearAllPools();

        // 4. Read with wrong password must fail
        var wrongPassBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Password = "WrongPassword",
            Mode = SqliteOpenMode.ReadWrite
        };

        using (var connection = new SqliteConnection(wrongPassBuilder.ConnectionString))
        {
            Action act = () =>
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM SecretData;";
                cmd.ExecuteScalar();
            };

            act.Should().Throw<SqliteException>();
        }
    }

    [Fact]
    public void EncryptExisting_WhenDatabaseIsPlain_ConvertsToEncryptedFormat()
    {
        // 1. Create unencrypted database
        var unencryptedBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        using (var connection = new SqliteConnection(unencryptedBuilder.ConnectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE PlainData (Id INTEGER PRIMARY KEY, Note TEXT); INSERT INTO PlainData (Note) VALUES ('Confidential plain data');";
            cmd.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();

        // 2. Open unencrypted and execute PRAGMA rekey = '...'
        var newPass = "NewMasterPassphrase456!";
        using (var connection = new SqliteConnection(unencryptedBuilder.ConnectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA rekey = '{newPass}';";
            cmd.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();

        // 3. Opening without password now fails
        using (var connection = new SqliteConnection(unencryptedBuilder.ConnectionString))
        {
            Action act = () =>
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM PlainData;";
                cmd.ExecuteScalar();
            };

            act.Should().Throw<SqliteException>();
        }

        SqliteConnection.ClearAllPools();

        // 4. Opening with new password succeeds and preserves data
        var encryptedBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Password = newPass,
            Mode = SqliteOpenMode.ReadWrite
        };

        using (var connection = new SqliteConnection(encryptedBuilder.ConnectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Note FROM PlainData WHERE Id = 1;";
            var note = (string?)cmd.ExecuteScalar();
            note.Should().Be("Confidential plain data");
        }
    }

    [Fact]
    public void ChangePassword_WhenNewKeyProvided_UpdatesEncryptionKeySuccessfully()
    {
        var oldPass = "InitialPass789!";
        var newPass = "UpdatedPass987!";

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Password = oldPass,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        using (var connection = new SqliteConnection(builder.ConnectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE Info (Text TEXT); INSERT INTO Info VALUES ('KeepSafe');";
            cmd.ExecuteNonQuery();

            // Rekey to new password
            cmd.CommandText = $"PRAGMA rekey = '{newPass}';";
            cmd.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();

        // Opening with old password must fail
        using (var connection = new SqliteConnection(builder.ConnectionString))
        {
            Action act = () =>
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Info;";
                cmd.ExecuteScalar();
            };

            act.Should().Throw<SqliteException>();
        }

        SqliteConnection.ClearAllPools();

        // Opening with new password must succeed
        builder.Password = newPass;
        using (var connection = new SqliteConnection(builder.ConnectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Text FROM Info;";
            var result = (string?)cmd.ExecuteScalar();
            result.Should().Be("KeepSafe");
        }
    }

    [Fact]
    public void DetectEncryption_WhenInspectingHeader_DistinguishesPlainFromEncrypted()
    {
        // 1. Unencrypted
        var plainBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        using (var conn = new SqliteConnection(plainBuilder.ConnectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE T (Id INT);";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        byte[] header = new byte[16];
        using (var stream = File.OpenRead(_testDbPath))
        {
            stream.ReadExactly(header, 0, 16);
        }
        System.Text.Encoding.ASCII.GetString(header).Should().StartWith("SQLite format 3");

        // 2. Encrypt it
        using (var conn = new SqliteConnection(plainBuilder.ConnectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA rekey = 'MySecret123';";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        using (var stream = File.OpenRead(_testDbPath))
        {
            stream.ReadExactly(header, 0, 16);
        }
        System.Text.Encoding.ASCII.GetString(header).Should().NotStartWith("SQLite format 3");
    }

    [Fact]
    public void DecryptDatabase_WhenEmptyPassphraseProvided_RestoresPlainSqliteFormat()
    {
        var pass = "TempPass123!";
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Password = pass,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        using (var conn = new SqliteConnection(builder.ConnectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE T (Name TEXT); INSERT INTO T VALUES ('TestPlain');";
            cmd.ExecuteNonQuery();

            // Decrypt back to unencrypted: PRAGMA rekey = ''
            cmd.CommandText = "PRAGMA rekey = '';";
            cmd.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();

        // Should now be openable without password
        var plainBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Mode = SqliteOpenMode.ReadOnly
        };

        using (var conn = new SqliteConnection(plainBuilder.ConnectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Name FROM T;";
            var val = (string?)cmd.ExecuteScalar();
            val.Should().Be("TestPlain");
        }

        byte[] header = new byte[16];
        using (var stream = File.OpenRead(_testDbPath))
        {
            stream.ReadExactly(header, 0, 16);
        }
        System.Text.Encoding.ASCII.GetString(header).Should().StartWith("SQLite format 3");
    }

    [Fact]
    public void OpenDatabase_WhenPassphraseHasSpecialCharsAndQuotes_AuthenticatesSuccessfully()
    {
        var complexPass = "P@$$w'o\"rd with sp@c€s & qu'otes!";
        var escapedPass = complexPass.Replace("'", "''");

        // 1. Create plain
        var plainBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        using (var conn = new SqliteConnection(plainBuilder.ConnectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE T (Msg TEXT); INSERT INTO T VALUES ('Special');";
            cmd.ExecuteNonQuery();

            cmd.CommandText = $"PRAGMA rekey = '{escapedPass}';";
            cmd.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();

        // 2. Open with complexPass in Password property
        var encBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Password = complexPass,
            Mode = SqliteOpenMode.ReadWrite
        };

        using (var conn = new SqliteConnection(encBuilder.ConnectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Msg FROM T;";
            var val = (string?)cmd.ExecuteScalar();
            val.Should().Be("Special");
        }
    }
}
