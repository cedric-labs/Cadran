using System;
using Ledger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetDateAndShiftToNextMonth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BudgetDate",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShiftToNextMonth",
                table: "CategorizationRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_BudgetDate",
                table: "Transactions",
                columns: new[] { "UserId", "BudgetDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId_BudgetDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BudgetDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ShiftToNextMonth",
                table: "CategorizationRules");
        }
    }
}
