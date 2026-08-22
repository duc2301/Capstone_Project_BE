using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PermissionAccountOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FolderPermissions_FolderId_ProjectParticipantId",
                table: "FolderPermissions");

            migrationBuilder.DropIndex(
                name: "IX_FilePermissions_FileItemId_ProjectParticipantId",
                table: "FilePermissions");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "FolderPermissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "FilePermissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_AccountId",
                table: "FolderPermissions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_FolderId_AccountId",
                table: "FolderPermissions",
                columns: new[] { "FolderId", "AccountId" },
                unique: true,
                filter: "\"AccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_FolderId_ProjectParticipantId",
                table: "FolderPermissions",
                columns: new[] { "FolderId", "ProjectParticipantId" },
                unique: true,
                filter: "\"ProjectParticipantId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FolderPermission_OneSubject",
                table: "FolderPermissions",
                sql: "(\"ProjectParticipantId\" IS NOT NULL) <> (\"AccountId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_FilePermissions_AccountId",
                table: "FilePermissions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FilePermissions_FileItemId_AccountId",
                table: "FilePermissions",
                columns: new[] { "FileItemId", "AccountId" },
                unique: true,
                filter: "\"AccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FilePermissions_FileItemId_ProjectParticipantId",
                table: "FilePermissions",
                columns: new[] { "FileItemId", "ProjectParticipantId" },
                unique: true,
                filter: "\"ProjectParticipantId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FilePermission_OneSubject",
                table: "FilePermissions",
                sql: "(\"ProjectParticipantId\" IS NOT NULL) <> (\"AccountId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_FilePermissions_Accounts_AccountId",
                table: "FilePermissions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FolderPermissions_Accounts_AccountId",
                table: "FolderPermissions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FilePermissions_Accounts_AccountId",
                table: "FilePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_FolderPermissions_Accounts_AccountId",
                table: "FolderPermissions");

            migrationBuilder.DropIndex(
                name: "IX_FolderPermissions_AccountId",
                table: "FolderPermissions");

            migrationBuilder.DropIndex(
                name: "IX_FolderPermissions_FolderId_AccountId",
                table: "FolderPermissions");

            migrationBuilder.DropIndex(
                name: "IX_FolderPermissions_FolderId_ProjectParticipantId",
                table: "FolderPermissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FolderPermission_OneSubject",
                table: "FolderPermissions");

            migrationBuilder.DropIndex(
                name: "IX_FilePermissions_AccountId",
                table: "FilePermissions");

            migrationBuilder.DropIndex(
                name: "IX_FilePermissions_FileItemId_AccountId",
                table: "FilePermissions");

            migrationBuilder.DropIndex(
                name: "IX_FilePermissions_FileItemId_ProjectParticipantId",
                table: "FilePermissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FilePermission_OneSubject",
                table: "FilePermissions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "FolderPermissions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "FilePermissions");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_FolderId_ProjectParticipantId",
                table: "FolderPermissions",
                columns: new[] { "FolderId", "ProjectParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FilePermissions_FileItemId_ProjectParticipantId",
                table: "FilePermissions",
                columns: new[] { "FileItemId", "ProjectParticipantId" },
                unique: true);
        }
    }
}
