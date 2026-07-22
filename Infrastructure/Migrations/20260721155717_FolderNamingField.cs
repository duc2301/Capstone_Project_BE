using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FolderNamingField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FolderNamingFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    NamingConventionFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FolderNamingFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FolderNamingFields_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FolderNamingFields_NamingConventionFields_NamingConventionF~",
                        column: x => x.NamingConventionFieldId,
                        principalTable: "NamingConventionFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FolderNamingFields_FolderId_NamingConventionFieldId",
                table: "FolderNamingFields",
                columns: new[] { "FolderId", "NamingConventionFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FolderNamingFields_NamingConventionFieldId",
                table: "FolderNamingFields",
                column: "NamingConventionFieldId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FolderNamingFields");
        }
    }
}
