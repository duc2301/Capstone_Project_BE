using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoiProjectAliasesAndSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoiFieldAliases_AliasNormalized",
                table: "LoiFieldAliases");

            migrationBuilder.DropIndex(
                name: "IX_LoiFieldAliases_FieldNameNormalized_AliasNormalized",
                table: "LoiFieldAliases");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "LoiFieldAliases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByAccountId",
                table: "LoiFieldAliases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "LoiFieldAliases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnmappedSummaryJson",
                table: "FileVersionLoiChecks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoiFieldAliases_ProjectId_AliasNormalized",
                table: "LoiFieldAliases",
                columns: new[] { "ProjectId", "AliasNormalized" });

            migrationBuilder.CreateIndex(
                name: "IX_LoiFieldAliases_ProjectId_FieldNameNormalized_AliasNormaliz~",
                table: "LoiFieldAliases",
                columns: new[] { "ProjectId", "FieldNameNormalized", "AliasNormalized" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoiFieldAliases_ProjectId_AliasNormalized",
                table: "LoiFieldAliases");

            migrationBuilder.DropIndex(
                name: "IX_LoiFieldAliases_ProjectId_FieldNameNormalized_AliasNormaliz~",
                table: "LoiFieldAliases");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "LoiFieldAliases");

            migrationBuilder.DropColumn(
                name: "CreatedByAccountId",
                table: "LoiFieldAliases");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "LoiFieldAliases");

            migrationBuilder.DropColumn(
                name: "UnmappedSummaryJson",
                table: "FileVersionLoiChecks");

            migrationBuilder.CreateIndex(
                name: "IX_LoiFieldAliases_AliasNormalized",
                table: "LoiFieldAliases",
                column: "AliasNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_LoiFieldAliases_FieldNameNormalized_AliasNormalized",
                table: "LoiFieldAliases",
                columns: new[] { "FieldNameNormalized", "AliasNormalized" },
                unique: true);
        }
    }
}
