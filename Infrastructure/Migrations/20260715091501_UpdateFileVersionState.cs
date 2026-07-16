using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFileVersionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileVersionStates_FileItemId",
                table: "FileVersionStates");

            migrationBuilder.AddColumn<string>(
                name: "Checksum",
                table: "FileVersionStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "FileVersionStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "FileVersionStates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FileVersionId",
                table: "FileVersionStates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "FileVersionStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                table: "FileVersionStates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "FileVersionStates",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileVersionStates_FileItemId",
                table: "FileVersionStates",
                column: "FileItemId",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_FileVersionStates_FileItemId_IsCurrent",
                table: "FileVersionStates",
                columns: new[] { "FileItemId", "IsCurrent" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileVersionStates_FileItemId",
                table: "FileVersionStates");

            migrationBuilder.DropIndex(
                name: "IX_FileVersionStates_FileItemId_IsCurrent",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "Checksum",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "FileVersionId",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "FileVersionStates");

            migrationBuilder.CreateIndex(
                name: "IX_FileVersionStates_FileItemId",
                table: "FileVersionStates",
                column: "FileItemId",
                unique: true);
        }
    }
}
