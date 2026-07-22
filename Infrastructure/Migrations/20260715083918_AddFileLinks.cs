using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateTable(
                name: "FileLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedFileItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileLinks_FileItems_FileItemId",
                        column: x => x.FileItemId,
                        principalTable: "FileItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileLinks_FileItems_LinkedFileItemId",
                        column: x => x.LinkedFileItemId,
                        principalTable: "FileItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileLinks_FileItemId_LinkedFileItemId",
                table: "FileLinks",
                columns: new[] { "FileItemId", "LinkedFileItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileLinks_LinkedFileItemId",
                table: "FileLinks",
                column: "LinkedFileItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileLinks");

            migrationBuilder.AddColumn<Guid>(
                name: "FileRelationId",
                table: "FileItems",
                type: "uuid",
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
    }
}
