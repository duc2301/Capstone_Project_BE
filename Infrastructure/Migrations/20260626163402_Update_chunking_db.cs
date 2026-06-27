using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Update_chunking_db : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_ProjectId_IsLatest",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsLatest",
                table: "Documents");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateAt",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_UpdateAt",
                table: "Documents",
                columns: new[] { "ProjectId", "UpdateAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_ProjectId_UpdateAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "UpdateAt",
                table: "Documents");

            migrationBuilder.AddColumn<bool>(
                name: "IsLatest",
                table: "Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_IsLatest",
                table: "Documents",
                columns: new[] { "ProjectId", "IsLatest" });
        }
    }
}
