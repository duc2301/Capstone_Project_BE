using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrganizationFromFolderPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FolderPermissions_Organizations_OrganizationId",
                table: "FolderPermissions");

            migrationBuilder.DropIndex(
                name: "IX_FolderPermissions_OrganizationId",
                table: "FolderPermissions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "FolderPermissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "FolderPermissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_OrganizationId",
                table: "FolderPermissions",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_FolderPermissions_Organizations_OrganizationId",
                table: "FolderPermissions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
