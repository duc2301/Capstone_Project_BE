using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeRelationshipFileFolderPermissionFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FilePermissions_Groups_GroupId",
                table: "FilePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_FolderPermissions_Groups_GroupId",
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

            migrationBuilder.DropColumn(
                name: "OwnerOrganizationId",
                table: "Folders");

            migrationBuilder.RenameColumn(
                name: "GroupId",
                table: "FolderPermissions",
                newName: "ProjectParticipantId");

            migrationBuilder.RenameIndex(
                name: "IX_FolderPermissions_GroupId",
                table: "FolderPermissions",
                newName: "IX_FolderPermissions_ProjectParticipantId");

            migrationBuilder.RenameColumn(
                name: "GroupId",
                table: "FilePermissions",
                newName: "ProjectParticipantId");

            migrationBuilder.RenameIndex(
                name: "IX_FilePermissions_GroupId",
                table: "FilePermissions",
                newName: "IX_FilePermissions_ProjectParticipantId");

            migrationBuilder.AddForeignKey(
                name: "FK_FilePermissions_ProjectParticipants_ProjectParticipantId",
                table: "FilePermissions",
                column: "ProjectParticipantId",
                principalTable: "ProjectParticipants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FolderPermissions_ProjectParticipants_ProjectParticipantId",
                table: "FolderPermissions",
                column: "ProjectParticipantId",
                principalTable: "ProjectParticipants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FilePermissions_ProjectParticipants_ProjectParticipantId",
                table: "FilePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_FolderPermissions_ProjectParticipants_ProjectParticipantId",
                table: "FolderPermissions");

            migrationBuilder.RenameColumn(
                name: "ProjectParticipantId",
                table: "FolderPermissions",
                newName: "GroupId");

            migrationBuilder.RenameIndex(
                name: "IX_FolderPermissions_ProjectParticipantId",
                table: "FolderPermissions",
                newName: "IX_FolderPermissions_GroupId");

            migrationBuilder.RenameColumn(
                name: "ProjectParticipantId",
                table: "FilePermissions",
                newName: "GroupId");

            migrationBuilder.RenameIndex(
                name: "IX_FilePermissions_ProjectParticipantId",
                table: "FilePermissions",
                newName: "IX_FilePermissions_GroupId");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerGroupId",
                table: "Folders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerOrganizationId",
                table: "Folders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Folders_OwnerGroupId",
                table: "Folders",
                column: "OwnerGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_FilePermissions_Groups_GroupId",
                table: "FilePermissions",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FolderPermissions_Groups_GroupId",
                table: "FolderPermissions",
                column: "GroupId",
                principalTable: "Groups",
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
    }
}
