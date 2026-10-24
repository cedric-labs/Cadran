using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSalaryToCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSalary",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSalary",
                table: "Categories");
        }
    }
}
