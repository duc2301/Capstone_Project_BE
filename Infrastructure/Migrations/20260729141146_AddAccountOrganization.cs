using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SectionsJson",
                table: "FileVersionLoiChecks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrganizationId",
                table: "Accounts",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Organizations_OrganizationId",
                table: "Accounts",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Organizations_OrganizationId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_OrganizationId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SectionsJson",
                table: "FileVersionLoiChecks");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Accounts");
        }
    }
}
