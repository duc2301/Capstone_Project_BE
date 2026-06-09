using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Folder_OwnerGroup_And_PermissionFks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FolderPermissions_Groups_GroupId",
                table: "FolderPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_FolderPermissions_Organizations_OrganizationId",
                table: "FolderPermissions");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerGroupId",
                table: "Folders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Folders_OwnerGroupId",
                table: "Folders",
                column: "OwnerGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_FolderPermissions_Groups_GroupId",
                table: "FolderPermissions",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FolderPermissions_Organizations_OrganizationId",
                table: "FolderPermissions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Folders_Groups_OwnerGroupId",
                table: "Folders",
                column: "OwnerGroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FolderPermissions_Groups_GroupId",
                table: "FolderPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_FolderPermissions_Organizations_OrganizationId",
                table: "FolderPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Folders_Groups_OwnerGroupId",
                table: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_Folders_OwnerGroupId",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "OwnerGroupId",
                table: "Folders");

            migrationBuilder.AddForeignKey(
                name: "FK_FolderPermissions_Groups_GroupId",
                table: "FolderPermissions",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FolderPermissions_Organizations_OrganizationId",
                table: "FolderPermissions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id");
        }
    }
}
