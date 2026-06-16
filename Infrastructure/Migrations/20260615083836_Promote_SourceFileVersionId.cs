using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Promote_SourceFileVersionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceFileVersionId",
                table: "FileVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileVersions_SourceFileVersionId",
                table: "FileVersions",
                column: "SourceFileVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileVersions_FileVersions_SourceFileVersionId",
                table: "FileVersions",
                column: "SourceFileVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileVersions_FileVersions_SourceFileVersionId",
                table: "FileVersions");

            migrationBuilder.DropIndex(
                name: "IX_FileVersions_SourceFileVersionId",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "SourceFileVersionId",
                table: "FileVersions");
        }
    }
}
