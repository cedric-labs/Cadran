using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantEntityAndRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""EntityTranslations"" (
    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_EntityTranslations"" PRIMARY KEY,
    ""EntityType"" TEXT NOT NULL,
    ""EntityId"" TEXT NOT NULL,
    ""LanguageCode"" TEXT NOT NULL,
    ""Field"" TEXT NOT NULL,
    ""Translation"" TEXT NOT NULL,
    ""CreatedAtUtc"" TEXT NOT NULL,
    ""UpdatedAtUtc"" TEXT NULL
);

CREATE TABLE IF NOT EXISTS ""Merchants"" (
    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Merchants"" PRIMARY KEY,
    ""UserId"" TEXT NOT NULL,
    ""Name"" TEXT NOT NULL,
    ""NormalizedName"" TEXT NOT NULL,
    ""DefaultCategoryId"" TEXT NULL,
    ""IconName"" TEXT NULL DEFAULT 'bi-shop',
    ""ColorHex"" TEXT NULL DEFAULT '#6366f1',
    ""CreatedAtUtc"" TEXT NOT NULL,
    ""UpdatedAtUtc"" TEXT NULL,
    CONSTRAINT ""FK_Merchants_Categories_DefaultCategoryId"" FOREIGN KEY (""DefaultCategoryId"") REFERENCES ""Categories"" (""Id"") ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS ""RecurringExpenses"" (
    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_RecurringExpenses"" PRIMARY KEY,
    ""UserId"" TEXT NOT NULL,
    ""Name"" TEXT NOT NULL,
    ""Pattern"" TEXT NOT NULL,
    ""ExpectedAmount"" TEXT NOT NULL,
    ""AmountTolerancePercentage"" TEXT NOT NULL,
    ""Frequency"" INTEGER NOT NULL,
    ""ExpectedDayOfMonth"" INTEGER NULL,
    ""TargetCategoryId"" TEXT NULL,
    ""TargetBankAccountId"" TEXT NULL,
    ""IsActive"" INTEGER NOT NULL,
    ""AutoDetected"" INTEGER NOT NULL,
    ""LastDetectedDate"" TEXT NULL,
    ""LastDetectedAmount"" TEXT NULL,
    ""Notes"" TEXT NULL,
    ""CreatedAtUtc"" TEXT NOT NULL,
    ""UpdatedAtUtc"" TEXT NULL,
    CONSTRAINT ""FK_RecurringExpenses_BankAccounts_TargetBankAccountId"" FOREIGN KEY (""TargetBankAccountId"") REFERENCES ""BankAccounts"" (""Id"") ON DELETE SET NULL,
    CONSTRAINT ""FK_RecurringExpenses_Categories_TargetCategoryId"" FOREIGN KEY (""TargetCategoryId"") REFERENCES ""Categories"" (""Id"") ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS ""ReportDashboards"" (
    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_ReportDashboards"" PRIMARY KEY,
    ""UserId"" TEXT NOT NULL,
    ""Name"" TEXT NOT NULL,
    ""Description"" TEXT NULL,
    ""IsDefault"" INTEGER NOT NULL,
    ""DisplayOrder"" INTEGER NOT NULL,
    ""CreatedAtUtc"" TEXT NOT NULL,
    ""UpdatedAtUtc"" TEXT NULL
);

CREATE TABLE IF NOT EXISTS ""SavedPivotReports"" (
    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_SavedPivotReports"" PRIMARY KEY,
    ""UserId"" TEXT NOT NULL,
    ""Name"" TEXT NOT NULL,
    ""Description"" TEXT NULL,
    ""ConfigJson"" TEXT NOT NULL,
    ""IsDefault"" INTEGER NOT NULL,
    ""CreatedAtUtc"" TEXT NOT NULL,
    ""UpdatedAtUtc"" TEXT NULL
);

CREATE TABLE IF NOT EXISTS ""ReportWidgets"" (
    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_ReportWidgets"" PRIMARY KEY,
    ""DashboardId"" TEXT NOT NULL,
    ""Title"" TEXT NOT NULL,
    ""WidgetType"" TEXT NOT NULL,
    ""WidthCols"" INTEGER NOT NULL,
    ""DisplayOrder"" INTEGER NOT NULL,
    ""ConfigJson"" TEXT NOT NULL,
    ""CreatedAtUtc"" TEXT NOT NULL,
    ""UpdatedAtUtc"" TEXT NULL,
    CONSTRAINT ""FK_ReportWidgets_ReportDashboards_DashboardId"" FOREIGN KEY (""DashboardId"") REFERENCES ""ReportDashboards"" (""Id"") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS ""IX_EntityTranslations_EntityType_EntityId_LanguageCode_Field"" ON ""EntityTranslations"" (""EntityType"", ""EntityId"", ""LanguageCode"", ""Field"");
CREATE INDEX IF NOT EXISTS ""IX_Merchants_DefaultCategoryId"" ON ""Merchants"" (""DefaultCategoryId"");
CREATE INDEX IF NOT EXISTS ""IX_Merchants_UserId_NormalizedName"" ON ""Merchants"" (""UserId"", ""NormalizedName"");
CREATE INDEX IF NOT EXISTS ""IX_RecurringExpenses_TargetBankAccountId"" ON ""RecurringExpenses"" (""TargetBankAccountId"");
CREATE INDEX IF NOT EXISTS ""IX_RecurringExpenses_TargetCategoryId"" ON ""RecurringExpenses"" (""TargetCategoryId"");
CREATE INDEX IF NOT EXISTS ""IX_RecurringExpenses_UserId_Name"" ON ""RecurringExpenses"" (""UserId"", ""Name"");
CREATE INDEX IF NOT EXISTS ""IX_ReportDashboards_UserId_DisplayOrder"" ON ""ReportDashboards"" (""UserId"", ""DisplayOrder"");
CREATE INDEX IF NOT EXISTS ""IX_ReportWidgets_DashboardId"" ON ""ReportWidgets"" (""DashboardId"");
CREATE INDEX IF NOT EXISTS ""IX_SavedPivotReports_UserId"" ON ""SavedPivotReports"" (""UserId"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"ReportWidgets\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"SavedPivotReports\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"ReportDashboards\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"RecurringExpenses\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"Merchants\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"EntityTranslations\";");
        }
    }
}
