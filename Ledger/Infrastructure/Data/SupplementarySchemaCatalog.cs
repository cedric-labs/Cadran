using System.Collections.Immutable;

namespace Ledger.Infrastructure.Data;

/// <summary>
/// Colonnes, tables et index complémentaires aux migrations EF Core, créés de façon idempotente
/// pour réparer les bases dont l'historique de migrations est incomplet.
/// </summary>
internal static class SupplementarySchemaCatalog
{
    /// <summary>Colonnes à ajouter aux tables existantes si elles sont absentes.</summary>
    public static ImmutableArray<SupplementaryColumn> Columns { get; } =
    [
        new("Transactions", "BudgetDate", "TEXT"),
        new("CategorizationRules", "ShiftToNextMonth", "INTEGER NOT NULL DEFAULT 0"),
        new("Transactions", "CustomStatusId", "TEXT"),
        new("Categories", "IsSalary", "INTEGER NOT NULL DEFAULT 0"),
        new("BankAccounts", "CustomAccountTypeId", "TEXT"),
        new("BankAccounts", "OfxAccountId", "TEXT"),
        new("Transactions", "MerchantId", "TEXT"),
        new("CategorizationRules", "TargetMerchantId", "TEXT"),
        new("Transactions", "IsSplit", "INTEGER NOT NULL DEFAULT 0"),
    ];

    /// <summary>Instructions de création de tables et d'index, dans l'ordre d'exécution (les index suivent leur table).</summary>
    public static ImmutableArray<string> TableAndIndexStatements { get; } =
    [
        "CREATE INDEX IF NOT EXISTS IX_Transactions_MerchantId ON Transactions (MerchantId);",
        "CREATE INDEX IF NOT EXISTS IX_CategorizationRules_TargetMerchantId ON CategorizationRules (TargetMerchantId);",
        @"CREATE TABLE IF NOT EXISTS Merchants (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Name TEXT NOT NULL,
            NormalizedName TEXT NOT NULL,
            DefaultCategoryId TEXT,
            IconName TEXT,
            ColorHex TEXT,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT
        );",
        "CREATE INDEX IF NOT EXISTS IX_Merchants_UserId_NormalizedName ON Merchants (UserId, NormalizedName);",
        @"CREATE TABLE IF NOT EXISTS TransactionSplits (
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
        );",
        "CREATE INDEX IF NOT EXISTS IX_TransactionSplits_TransactionId ON TransactionSplits (TransactionId);",
        "CREATE INDEX IF NOT EXISTS IX_TransactionSplits_UserId_TransactionId ON TransactionSplits (UserId, TransactionId);",
        @"CREATE TABLE IF NOT EXISTS CustomAccountTypes (
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
        );",
        @"CREATE TABLE IF NOT EXISTS TransactionStatuses (
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
        );",
        "CREATE INDEX IF NOT EXISTS IX_TransactionStatuses_UserId_Name ON TransactionStatuses (UserId, Name);",
        @"CREATE TABLE IF NOT EXISTS UiFonts (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            FontFamilyCss TEXT NOT NULL,
            GoogleFontUrl TEXT,
            IsActive INTEGER NOT NULL,
            IsSystem INTEGER NOT NULL,
            DisplayOrder INTEGER NOT NULL
        );",
        @"CREATE TABLE IF NOT EXISTS EntityTranslations (
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
        ON EntityTranslations (EntityType, EntityId, LanguageCode, Field);",
        @"CREATE TABLE IF NOT EXISTS RecurringExpenses (
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
        );",
        "CREATE INDEX IF NOT EXISTS IX_RecurringExpenses_UserId_Name ON RecurringExpenses (UserId, Name);",
        @"CREATE TABLE IF NOT EXISTS ReportDashboards (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Description TEXT,
            IsDefault INTEGER NOT NULL DEFAULT 0,
            DisplayOrder INTEGER NOT NULL DEFAULT 1,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT
        );",
        "CREATE INDEX IF NOT EXISTS IX_ReportDashboards_UserId_Order ON ReportDashboards (UserId, DisplayOrder);",
        @"CREATE TABLE IF NOT EXISTS ReportWidgets (
            Id TEXT PRIMARY KEY,
            DashboardId TEXT NOT NULL,
            Title TEXT NOT NULL,
            WidgetType TEXT NOT NULL,
            WidthCols INTEGER NOT NULL DEFAULT 6,
            DisplayOrder INTEGER NOT NULL DEFAULT 1,
            ConfigJson TEXT NOT NULL DEFAULT '{}',
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT
        );",
        "CREATE INDEX IF NOT EXISTS IX_ReportWidgets_DashboardId ON ReportWidgets (DashboardId);",
        @"CREATE TABLE IF NOT EXISTS SavedPivotReports (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Description TEXT,
            ConfigJson TEXT NOT NULL,
            IsDefault INTEGER NOT NULL DEFAULT 0,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT
        );",
        "CREATE INDEX IF NOT EXISTS IX_SavedPivotReports_UserId ON SavedPivotReports (UserId);",
    ];
}

/// <summary>Colonne complémentaire d'une table : nom de table, nom de colonne et définition SQLite.</summary>
internal sealed record SupplementaryColumn(string TableName, string ColumnName, string Definition);
