using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarkupViewpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarkupSvg",
                table: "FileNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailDataUrl",
                table: "FileNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewpointStateJson",
                table: "FileNotes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkupSvg",
                table: "FileNotes");

            migrationBuilder.DropColumn(
                name: "ThumbnailDataUrl",
                table: "FileNotes");

            migrationBuilder.DropColumn(
                name: "ViewpointStateJson",
                table: "FileNotes");
        }
    }
}
