using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanDownload",
                table: "FolderPermissions");

            migrationBuilder.DropColumn(
                name: "CanUpdate",
                table: "FolderPermissions");

            migrationBuilder.DropColumn(
                name: "CanVerify",
                table: "FolderPermissions");

            migrationBuilder.DropColumn(
                name: "CanDownload",
                table: "FilePermissions");

            migrationBuilder.DropColumn(
                name: "CanUpdate",
                table: "FilePermissions");

            migrationBuilder.DropColumn(
                name: "CanVerify",
                table: "FilePermissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanDownload",
                table: "FolderPermissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanUpdate",
                table: "FolderPermissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanVerify",
                table: "FolderPermissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanDownload",
                table: "FilePermissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanUpdate",
                table: "FilePermissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanVerify",
                table: "FilePermissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
