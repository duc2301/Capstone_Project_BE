using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArchivedFileLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceFileItemId",
                table: "FileItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileItems_SourceFileItemId",
                table: "FileItems",
                column: "SourceFileItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileItems_FileItems_SourceFileItemId",
                table: "FileItems",
                column: "SourceFileItemId",
                principalTable: "FileItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileItems_FileItems_SourceFileItemId",
                table: "FileItems");

            migrationBuilder.DropIndex(
                name: "IX_FileItems_SourceFileItemId",
                table: "FileItems");

            migrationBuilder.DropColumn(
                name: "SourceFileItemId",
                table: "FileItems");
        }
    }
}
