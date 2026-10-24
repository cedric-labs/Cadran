using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionSplitEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""TransactionSplits"" (
    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_TransactionSplits"" PRIMARY KEY,
    ""UserId"" TEXT NOT NULL,
    ""TransactionId"" TEXT NOT NULL,
    ""Amount"" TEXT NOT NULL,
    ""CategoryId"" TEXT NULL,
    ""MerchantId"" TEXT NULL,
    ""Notes"" TEXT NULL,
    ""CreatedAtUtc"" TEXT NOT NULL,
    ""UpdatedAtUtc"" TEXT NULL,
    CONSTRAINT ""FK_TransactionSplits_Categories_CategoryId"" FOREIGN KEY (""CategoryId"") REFERENCES ""Categories"" (""Id"") ON DELETE SET NULL,
    CONSTRAINT ""FK_TransactionSplits_Merchants_MerchantId"" FOREIGN KEY (""MerchantId"") REFERENCES ""Merchants"" (""Id"") ON DELETE SET NULL,
    CONSTRAINT ""FK_TransactionSplits_Transactions_TransactionId"" FOREIGN KEY (""TransactionId"") REFERENCES ""Transactions"" (""Id"") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ""IX_TransactionSplits_CategoryId"" ON ""TransactionSplits"" (""CategoryId"");
CREATE INDEX IF NOT EXISTS ""IX_TransactionSplits_MerchantId"" ON ""TransactionSplits"" (""MerchantId"");
CREATE INDEX IF NOT EXISTS ""IX_TransactionSplits_TransactionId"" ON ""TransactionSplits"" (""TransactionId"");
CREATE INDEX IF NOT EXISTS ""IX_TransactionSplits_UserId_TransactionId"" ON ""TransactionSplits"" (""UserId"", ""TransactionId"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"TransactionSplits\";");
        }
    }
}
