using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Rag_ParentChild_FTS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentChunks_Documents_DocumentId",
                table: "DocumentChunks");

            migrationBuilder.AddColumn<string>(
                name: "Discipline",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLatest",
                table: "Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Revision",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentChunkId",
                table: "DocumentChunks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "DocumentParentChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    SectionTitle = table.Column<string>(type: "text", nullable: true),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentParentChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentParentChunks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_IsLatest",
                table: "Documents",
                columns: new[] { "ProjectId", "IsLatest" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_ParentChunkId",
                table: "DocumentChunks",
                column: "ParentChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentParentChunks_DocumentId",
                table: "DocumentParentChunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentParentChunks_ProjectId",
                table: "DocumentParentChunks",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentChunks_DocumentParentChunks_ParentChunkId",
                table: "DocumentChunks",
                column: "ParentChunkId",
                principalTable: "DocumentParentChunks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentChunks_DocumentParentChunks_ParentChunkId",
                table: "DocumentChunks");

            migrationBuilder.DropTable(
                name: "DocumentParentChunks");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ProjectId_IsLatest",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_ParentChunkId",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "Discipline",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsLatest",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ParentChunkId",
                table: "DocumentChunks");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentChunks_Documents_DocumentId",
                table: "DocumentChunks",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
