using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Service assurant la synchronisation du schéma SQLite, l'application des migrations EF Core,
/// la création idempotente des tables/colonnes complémentaires et la normalisation des GUIDs.
/// </summary>
public sealed class DatabaseSchemaSynchronizer(ILogger<DatabaseSchemaSynchronizer> logger)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Synchronise l'ensemble du schéma de la base de données SQLite : migrations, colonnes et tables complémentaires.
    /// </summary>
    /// <param name="context">Contexte de base de données.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    public async Task SynchronizeSchemaAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Application automatique des migrations EF Core (schéma complet) si nécessaire
        try
        {
            var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
            if (pendingMigrations.Count > 0)
            {
                await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            logger.LogWarning(ex, "Avertissement lors de la migration automatique EF Core (objet SQLite déjà existant). Réconciliation automatique en cours...");
            await ReconcilePendingMigrationsAsync(context, cancellationToken).ConfigureAwait(false);
        }

        // Sécurité colonnes pour SQLite (vérification préalable via PRAGMA table_info pour éviter les logs d'erreurs redondants)
        await EnsureColumnExistsAsync(context, "Transactions", "BudgetDate", "TEXT", cancellationToken).ConfigureAwait(false);
        await EnsureColumnExistsAsync(context, "CategorizationRules", "ShiftToNextMonth", "INTEGER NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await EnsureColumnExistsAsync(context, "Transactions", "CustomStatusId", "TEXT", cancellationToken).ConfigureAwait(false);
        await EnsureColumnExistsAsync(context, "Categories", "IsSalary", "INTEGER NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await EnsureColumnExistsAsync(context, "BankAccounts", "CustomAccountTypeId", "TEXT", cancellationToken).ConfigureAwait(false);
        await EnsureColumnExistsAsync(context, "BankAccounts", "OfxAccountId", "TEXT", cancellationToken).ConfigureAwait(false);
        await EnsureColumnExistsAsync(context, "Transactions", "MerchantId", "TEXT", cancellationToken).ConfigureAwait(false);
        await EnsureColumnExistsAsync(context, "CategorizationRules", "TargetMerchantId", "TEXT", cancellationToken).ConfigureAwait(false);
        await EnsureColumnExistsAsync(context, "Transactions", "IsSplit", "INTEGER NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);

        await EnsureTableCreatedAsync(context, "CREATE INDEX IF NOT EXISTS IX_Transactions_MerchantId ON Transactions (MerchantId);", cancellationToken).ConfigureAwait(false);
        await EnsureTableCreatedAsync(context, "CREATE INDEX IF NOT EXISTS IX_CategorizationRules_TargetMerchantId ON CategorizationRules (TargetMerchantId);", cancellationToken).ConfigureAwait(false);

        await EnsureTableCreatedAsync(context, @"CREATE TABLE IF NOT EXISTS Merchants (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Name TEXT NOT NULL,
            NormalizedName TEXT NOT NULL,
            DefaultCategoryId TEXT,
            IconName TEXT,
            ColorHex TEXT,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT
        );", cancellationToken).ConfigureAwait(false);
        await EnsureTableCreatedAsync(context, "CREATE INDEX IF NOT EXISTS IX_Merchants_UserId_NormalizedName ON Merchants (UserId, NormalizedName);", cancellationToken).ConfigureAwait(false);

        await EnsureTableCreatedAsync(context, @"CREATE TABLE IF NOT EXISTS TransactionSplits (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            TransactionId TEXT NOT NULL,
            Amount TEXT NOT NULL,
            CategoryId TEXT,
            MerchantId TEXT,
            Notes TEXT,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT,
            CONSTRAINT FK_TransactionSplits_Transactions_TransactionId FOREIGN KEY (TransactionId) REFERENCES Transactions (Id) ON DELETE CASCADE
        );", cancellationToken).ConfigureAwait(false);
        await EnsureTableCreatedAsync(context, "CREATE INDEX IF NOT EXISTS IX_TransactionSplits_TransactionId ON TransactionSplits (TransactionId);", cancellationToken).ConfigureAwait(false);
        await EnsureTableCreatedAsync(context, "CREATE INDEX IF NOT EXISTS IX_TransactionSplits_UserId_TransactionId ON TransactionSplits (UserId, TransactionId);", cancellationToken).ConfigureAwait(false);

        await EnsureTableCreatedAsync(context, @"CREATE TABLE IF NOT EXISTS CustomAccountTypes (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Code TEXT,
            Description TEXT,
            CategoryGroup INTEGER NOT NULL,
            ColorHex TEXT NOT NULL,
            IconName TEXT NOT NULL,
            IsSystem INTEGER NOT NULL,
            DisplayOrder INTEGER NOT NULL
        );", cancellationToken).ConfigureAwait(false);

        await EnsureTableCreatedAsync(context, @"CREATE TABLE IF NOT EXISTS TransactionStatuses (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Code TEXT,
            Description TEXT,
            ColorHex TEXT NOT NULL DEFAULT '#3b82f6',
            IconName TEXT NOT NULL DEFAULT 'bi-check-circle',
            IsSystem INTEGER NOT NULL DEFAULT 0,
            IsDefault INTEGER NOT NULL DEFAULT 0,
            DisplayOrder INTEGER NOT NULL DEFAULT 1,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT
        );", cancellationToken).ConfigureAwait(false);
        await EnsureTableCreatedAsync(context, "CREATE INDEX IF NOT EXISTS IX_TransactionStatuses_UserId_Name ON TransactionStatuses (UserId, Name);", cancellationToken).ConfigureAwait(false);

        await EnsureTableCreatedAsync(context, @"CREATE TABLE IF NOT EXISTS UiFonts (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            FontFamilyCss TEXT NOT NULL,
            GoogleFontUrl TEXT,
            IsActive INTEGER NOT NULL,
            IsSystem INTEGER NOT NULL,
            DisplayOrder INTEGER NOT NULL
        );", cancellationToken).ConfigureAwait(false);

        await EnsureTableCreatedAsync(context, @"CREATE TABLE IF NOT EXISTS EntityTranslations (
            Id TEXT PRIMARY KEY,
            EntityType TEXT NOT NULL,
            EntityId TEXT NOT NULL,
            LanguageCode TEXT NOT NULL,
            Field TEXT NOT NULL,
            Translation TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_EntityTranslations_Lookup 
        ON EntityTranslations (EntityType, EntityId, LanguageCode, Field);", cancellationToken).ConfigureAwait(false);

        await EnsureTableCreatedAsync(context, @"CREATE TABLE IF NOT EXISTS RecurringExpenses (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Pattern TEXT NOT NULL,
            ExpectedAmount REAL NOT NULL,
            AmountTolerancePercentage REAL NOT NULL DEFAULT 0.05,
            Frequency INTEGER NOT NULL DEFAULT 1,
            ExpectedDayOfMonth INTEGER,
            TargetCategoryId TEXT,
            TargetBankAccountId TEXT,
            IsActive INTEGER NOT NULL DEFAULT 1,
            AutoDetected INTEGER NOT NULL DEFAULT 0,
            LastDetectedDate TEXT,
            LastDetectedAmount REAL,
            Notes TEXT,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT
        );", cancellationToken).ConfigureAwait(false);
        await EnsureTableCreatedAsync(context, "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_UserId_Name ON RecurringExpenses (UserId, Name);", cancellationToken).ConfigureAwait(false);

        await EnsureTableCreatedAsync(context, @"CREATE TABLE IF NOT EXISTS ReportDashboards (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Description TEXT,
            IsDefault INTEGER NOT NULL DEFAULT 0,
            DisplayOrder INTEGER NOT NULL DEFAULT 1,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT
        );", cancellationToken).ConfigureAwait(false);
        await EnsureTableCreatedAsync(context, "CREATE INDEX IF NOT EXISTS IX_ReportDashboards_UserId_Order ON ReportDashboards (UserId, DisplayOrder);", cancellationToken).ConfigureAwait(false);

        await EnsureTableCreatedAsync(context, @"CREATE TABLE IF NOT EXISTS ReportWidgets (
            Id TEXT PRIMARY KEY,
            DashboardId TEXT NOT NULL,
            Title TEXT NOT NULL,
            WidgetType TEXT NOT NULL,
            WidthCols INTEGER NOT NULL DEFAULT 6,
            DisplayOrder INTEGER NOT NULL DEFAULT 1,
            ConfigJson TEXT NOT NULL DEFAULT '{}',
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT
        );", cancellationToken).ConfigureAwait(false);
        await EnsureTableCreatedAsync(context, "CREATE INDEX IF NOT EXISTS IX_ReportWidgets_DashboardId ON ReportWidgets (DashboardId);", cancellationToken).ConfigureAwait(false);

        await EnsureTableCreatedAsync(context, @"CREATE TABLE IF NOT EXISTS SavedPivotReports (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Description TEXT,
            ConfigJson TEXT NOT NULL,
            IsDefault INTEGER NOT NULL DEFAULT 0,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT
        );", cancellationToken).ConfigureAwait(false);
        await EnsureTableCreatedAsync(context, "CREATE INDEX IF NOT EXISTS IX_SavedPivotReports_UserId ON SavedPivotReports (UserId);", cancellationToken).ConfigureAwait(false);

        // Configuration du mode WAL et pragmas SQLite
        await ApplyDatabasePragmasAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Applique les pragmas recommandés pour SQLite (mode WAL, synchronisation normale et timeout).
    /// </summary>
    public static async Task ApplyDatabasePragmasAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;", cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout = 5000;", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Normalise la casse des GUIDs stockés dans SQLite pour garantir la cohérence d'indexation.
    /// </summary>
    public static async Task NormalizeGuidCasingAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;", cancellationToken).ConfigureAwait(false);
        await SafeExecuteSqlAsync(context, "UPDATE Categories SET Id = UPPER(Id), ParentCategoryId = UPPER(ParentCategoryId) WHERE Id GLOB '*[a-z]*' OR ParentCategoryId GLOB '*[a-z]*';", cancellationToken).ConfigureAwait(false);
        await SafeExecuteSqlAsync(context, "UPDATE Transactions SET CategoryId = UPPER(CategoryId) WHERE CategoryId GLOB '*[a-z]*';", cancellationToken).ConfigureAwait(false);
        await SafeExecuteSqlAsync(context, "UPDATE CategorizationRules SET TargetCategoryId = UPPER(TargetCategoryId) WHERE TargetCategoryId GLOB '*[a-z]*';", cancellationToken).ConfigureAwait(false);
        await SafeExecuteSqlAsync(context, "UPDATE EntityTranslations SET EntityId = UPPER(EntityId) WHERE EntityId GLOB '*[a-z]*';", cancellationToken).ConfigureAwait(false);
        await SafeExecuteSqlAsync(context, "UPDATE RecurringExpenses SET Id = UPPER(Id) WHERE Id GLOB '*[a-z]*';", cancellationToken).ConfigureAwait(false);
        await SafeExecuteSqlAsync(context, "UPDATE ReportDashboards SET Id = UPPER(Id) WHERE Id GLOB '*[a-z]*';", cancellationToken).ConfigureAwait(false);
        await SafeExecuteSqlAsync(context, "UPDATE ReportWidgets SET Id = UPPER(Id), DashboardId = UPPER(DashboardId) WHERE Id GLOB '*[a-z]*' OR DashboardId GLOB '*[a-z]*';", cancellationToken).ConfigureAwait(false);
        await SafeExecuteSqlAsync(context, "UPDATE SavedPivotReports SET Id = UPPER(Id) WHERE Id GLOB '*[a-z]*';", cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
    }

    private static Task EnsureTableCreatedAsync(AppDbContext context, string sql, CancellationToken cancellationToken)
    {
        return SafeExecuteSqlAsync(context, sql, cancellationToken);
    }

    private static async Task SafeExecuteSqlAsync(AppDbContext context, string sql, CancellationToken cancellationToken)
    {
        try
        {
            var connection = context.Database.GetDbConnection();
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                await ExecuteNonQueryCommandAsync(connection, sql, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (!wasOpen)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }
        }
        catch (DbException)
        {
            // Tolérance idempotente : la table, la colonne ou l'index cible peut déjà exister
        }
    }

    private static async Task EnsureColumnExistsAsync(AppDbContext context, string tableName, string columnName, string columnDefinition, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(tableName, @"^[a-zA-Z0-9_]+$", RegexOptions.None, RegexTimeout) ||
            !Regex.IsMatch(columnName, @"^[a-zA-Z0-9_]+$", RegexOptions.None, RegexTimeout) ||
            !Regex.IsMatch(columnDefinition, @"^[a-zA-Z0-9_\(\)\s'""]+$", RegexOptions.None, RegexTimeout))
        {
            return;
        }

        try
        {
            var connection = context.Database.GetDbConnection();
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var checkCmd = connection.CreateCommand();
                await using (checkCmd.ConfigureAwait(false))
                {
                    checkCmd.CommandText = @"
                    SELECT
                        (SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @tableName),
                        (SELECT COUNT(1) FROM pragma_table_info(@tableName) WHERE LOWER(name) = LOWER(@columnName));";

                    var tableParam = checkCmd.CreateParameter();
                    tableParam.ParameterName = "@tableName";
                    tableParam.Value = tableName;
                    checkCmd.Parameters.Add(tableParam);

                    var colParam = checkCmd.CreateParameter();
                    colParam.ParameterName = "@columnName";
                    colParam.Value = columnName;
                    checkCmd.Parameters.Add(colParam);

                    bool tableExists = false;
                    bool columnExists = false;
                    var reader = await checkCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            tableExists = reader.GetInt32(0) > 0;
                            columnExists = reader.GetInt32(1) > 0;
                        }
                    }

                    if (tableExists && !columnExists)
                    {
                        var alterStatement = string.Concat("ALTER TABLE \"", tableName, "\" ADD COLUMN \"", columnName, "\" ", columnDefinition, ";");
                        await ExecuteNonQueryCommandAsync(connection, alterStatement, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (!wasOpen)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }
        }
        catch (SqliteException)
        {
            // Table absente ou colonne déjà créée concurremment
        }
    }

    private static async Task ExecuteNonQueryCommandAsync(DbConnection connection, string ddlStatement, CancellationToken cancellationToken)
    {
        var cmd = connection.CreateCommand();
        await using (cmd.ConfigureAwait(false))
        {
            cmd.CommandText = ddlStatement;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReconcilePendingMigrationsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureTableCreatedAsync(context, @"CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY,
                ""ProductVersion"" TEXT NOT NULL
            );", cancellationToken).ConfigureAwait(false);

            var appliedMigrations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = context.Database.GetDbConnection();
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var cmd = connection.CreateCommand();
                await using (cmd.ConfigureAwait(false))
                {
                    cmd.CommandText = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\";";
                    var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            appliedMigrations.Add(reader.GetString(0));
                        }
                    }
                }
            }
            finally
            {
                if (!wasOpen)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }

            var allMigrations = context.Database.GetMigrations();
            foreach (var mig in allMigrations.Where(mig => !appliedMigrations.Contains(mig)))
            {
                logger.LogInformation("Réconciliation de la migration '{MigrationId}' dans __EFMigrationsHistory.", mig);
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1});",
                    new object[] { mig, "9.0.2" },
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception rex)
        {
            logger.LogError(rex, "Erreur lors de la réconciliation manuelle des migrations.");
        }
    }
}
