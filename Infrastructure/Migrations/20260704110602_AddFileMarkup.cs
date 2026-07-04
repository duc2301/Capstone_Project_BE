using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileMarkup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FileNotes cũ (nếu có) không có MarkupSetId hợp lệ; dữ liệu mẫu -> xoá để tránh vi phạm FK.
            migrationBuilder.Sql("DELETE FROM \"FileNotes\";");

            migrationBuilder.DropForeignKey(
                name: "FK_FileNotes_FileVersions_FileVersionId",
                table: "FileNotes");

            migrationBuilder.AlterColumn<string>(
                name: "CoordinateJson",
                table: "FileNotes",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "FileNotes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "MarkupSetId",
                table: "FileNotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "MarkupSvg",
                table: "FileNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarkupType",
                table: "FileNotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FileNotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StyleJson",
                table: "FileNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailDataUrl",
                table: "FileNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "FileNotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewpointStateJson",
                table: "FileNotes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarkupSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: true),
                    SnapshotStoragePath = table.Column<string>(type: "text", nullable: true),
                    CreatedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkupSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarkupSets_FileItems_FileItemId",
                        column: x => x.FileItemId,
                        principalTable: "FileItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarkupSets_FileVersions_FileVersionId",
                        column: x => x.FileVersionId,
                        principalTable: "FileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileNotes_MarkupSetId",
                table: "FileNotes",
                column: "MarkupSetId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkupSets_FileItemId",
                table: "MarkupSets",
                column: "FileItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkupSets_FileVersionId",
                table: "MarkupSets",
                column: "FileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkupSets_IssueId",
                table: "MarkupSets",
                column: "IssueId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileNotes_FileVersions_FileVersionId",
                table: "FileNotes",
                column: "FileVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FileNotes_MarkupSets_MarkupSetId",
                table: "FileNotes",
                column: "MarkupSetId",
                principalTable: "MarkupSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileNotes_FileVersions_FileVersionId",
                table: "FileNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_FileNotes_MarkupSets_MarkupSetId",
                table: "FileNotes");

            migrationBuilder.DropTable(
                name: "MarkupSets");

            migrationBuilder.DropIndex(
                name: "IX_FileNotes_MarkupSetId",
                table: "FileNotes");

            migrationBuilder.DropColumn(
                name: "MarkupSetId",
                table: "FileNotes");

            migrationBuilder.DropColumn(
                name: "MarkupSvg",
                table: "FileNotes");

            migrationBuilder.DropColumn(
                name: "MarkupType",
                table: "FileNotes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FileNotes");

            migrationBuilder.DropColumn(
                name: "StyleJson",
                table: "FileNotes");

            migrationBuilder.DropColumn(
                name: "ThumbnailDataUrl",
                table: "FileNotes");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "FileNotes");

            migrationBuilder.DropColumn(
                name: "ViewpointStateJson",
                table: "FileNotes");

            migrationBuilder.AlterColumn<string>(
                name: "CoordinateJson",
                table: "FileNotes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "FileNotes",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FileNotes_FileVersions_FileVersionId",
                table: "FileNotes",
                column: "FileVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
