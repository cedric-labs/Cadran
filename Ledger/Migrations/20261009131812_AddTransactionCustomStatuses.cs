using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionCustomStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomStatusId",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransactionStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false, defaultValue: "#3b82f6"),
                    IconName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "bi-check-circle"),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionStatuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CustomStatusId",
                table: "Transactions",
                column: "CustomStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionStatuses_UserId_Name",
                table: "TransactionStatuses",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_TransactionStatuses_CustomStatusId",
                table: "Transactions",
                column: "CustomStatusId",
                principalTable: "TransactionStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_TransactionStatuses_CustomStatusId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "TransactionStatuses");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CustomStatusId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CustomStatusId",
                table: "Transactions");
        }
    }
}
