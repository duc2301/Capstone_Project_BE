using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateContractPackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ContractPackages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ContractPackages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeDescription",
                table: "ContractPackages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "ContractPackages",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkTypes",
                table: "ContractPackages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ContractPackages");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ContractPackages");

            migrationBuilder.DropColumn(
                name: "ScopeDescription",
                table: "ContractPackages");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "ContractPackages");

            migrationBuilder.DropColumn(
                name: "WorkTypes",
                table: "ContractPackages");
        }
    }
}
