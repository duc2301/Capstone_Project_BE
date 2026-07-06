using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusForPermissionTablesAndDeleteInheritField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InheritFromParent",
                table: "FolderPermissions");

            migrationBuilder.DropColumn(
                name: "InheritFromParent",
                table: "FilePermissions");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FolderPermissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FilePermissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "FolderPermissions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FilePermissions");

            migrationBuilder.AddColumn<bool>(
                name: "InheritFromParent",
                table: "FolderPermissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InheritFromParent",
                table: "FilePermissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
