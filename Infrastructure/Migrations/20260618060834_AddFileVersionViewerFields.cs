using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileVersionViewerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviewStoragePath",
                table: "FileVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewerUrn",
                table: "FileVersions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewStoragePath",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "ViewerUrn",
                table: "FileVersions");
        }
    }
}
