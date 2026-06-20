using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileVersionViewerStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ViewerError",
                table: "FileVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewerProgress",
                table: "FileVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ViewerStatus",
                table: "FileVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ViewerError",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "ViewerProgress",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "ViewerStatus",
                table: "FileVersions");
        }
    }
}
