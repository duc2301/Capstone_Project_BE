using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class File_Relation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FileRelationId",
                table: "FileItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Warnning",
                table: "FileItems",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarnningMessage",
                table: "FileItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FileRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileRelations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileItems_FileRelationId",
                table: "FileItems",
                column: "FileRelationId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileItems_FileRelations_FileRelationId",
                table: "FileItems",
                column: "FileRelationId",
                principalTable: "FileRelations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileItems_FileRelations_FileRelationId",
                table: "FileItems");

            migrationBuilder.DropTable(
                name: "FileRelations");

            migrationBuilder.DropIndex(
                name: "IX_FileItems_FileRelationId",
                table: "FileItems");

            migrationBuilder.DropColumn(
                name: "FileRelationId",
                table: "FileItems");

            migrationBuilder.DropColumn(
                name: "Warnning",
                table: "FileItems");

            migrationBuilder.DropColumn(
                name: "WarnningMessage",
                table: "FileItems");
        }
    }
}
