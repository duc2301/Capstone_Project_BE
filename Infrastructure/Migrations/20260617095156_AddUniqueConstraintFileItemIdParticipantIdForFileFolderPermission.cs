using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintFileItemIdParticipantIdForFileFolderPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FolderPermissions_FolderId",
                table: "FolderPermissions");

            migrationBuilder.DropIndex(
                name: "IX_FilePermissions_FileItemId",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FolderPermissions_FolderId_ProjectParticipantId",
                table: "FolderPermissions");

            migrationBuilder.DropIndex(
                name: "IX_FilePermissions_FileItemId_ProjectParticipantId",
                table: "FilePermissions");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_FolderId",
                table: "FolderPermissions",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_FilePermissions_FileItemId",
                table: "FilePermissions",
                column: "FileItemId");
        }
    }
}
