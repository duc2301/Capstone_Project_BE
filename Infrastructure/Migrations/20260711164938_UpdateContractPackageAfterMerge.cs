using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateContractPackageAfterMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ContractSignDate",
                table: "PackageAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ContractPackages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentFolderId",
                table: "ContractPackages",
                type: "uuid",
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
                name: "ContractSignDate",
                table: "PackageAssignments");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ContractPackages");

            migrationBuilder.DropColumn(
                name: "DocumentFolderId",
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
