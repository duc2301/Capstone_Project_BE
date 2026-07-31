using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoiComponentTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ElementsNotCoveredByStandard",
                table: "FileVersionLoiChecks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NotCoveredSummaryJson",
                table: "FileVersionLoiChecks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoiComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Discipline = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CodeNormalized = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoiComponents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoiComponents_CodeNormalized",
                table: "LoiComponents",
                column: "CodeNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoiComponents");

            migrationBuilder.DropColumn(
                name: "ElementsNotCoveredByStandard",
                table: "FileVersionLoiChecks");

            migrationBuilder.DropColumn(
                name: "NotCoveredSummaryJson",
                table: "FileVersionLoiChecks");
        }
    }
}
