using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfSigningSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CertificateSerial",
                table: "FileVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSigned",
                table: "FileVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAt",
                table: "FileVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SignedBy",
                table: "FileVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SignedVersionId",
                table: "FileItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FileSignaturePositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: false),
                    X = table.Column<float>(type: "real", nullable: false),
                    Y = table.Column<float>(type: "real", nullable: false),
                    Width = table.Column<float>(type: "real", nullable: false),
                    Height = table.Column<float>(type: "real", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileSignaturePositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileSignaturePositions_FileItems_FileItemId",
                        column: x => x.FileItemId,
                        principalTable: "FileItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileSignaturePositions_FileItemId",
                table: "FileSignaturePositions",
                column: "FileItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileSignaturePositions");

            migrationBuilder.DropColumn(
                name: "CertificateSerial",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "IsSigned",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "SignedAt",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "SignedBy",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "SignedVersionId",
                table: "FileItems");
        }
    }
}
