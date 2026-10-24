using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomAccountTypesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomAccountTypeId",
                table: "BankAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomAccountTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    CategoryGroup = table.Column<int>(type: "INTEGER", nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false, defaultValue: "#0dcaf0"),
                    IconName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "bi-piggy-bank"),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAccountTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CustomAccountTypeId",
                table: "BankAccounts",
                column: "CustomAccountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomAccountTypes_UserId_Name",
                table: "CustomAccountTypes",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_CustomAccountTypes_CustomAccountTypeId",
                table: "BankAccounts",
                column: "CustomAccountTypeId",
                principalTable: "CustomAccountTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccounts_CustomAccountTypes_CustomAccountTypeId",
                table: "BankAccounts");

            migrationBuilder.DropTable(
                name: "CustomAccountTypes");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_CustomAccountTypeId",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "CustomAccountTypeId",
                table: "BankAccounts");
        }
    }
}
