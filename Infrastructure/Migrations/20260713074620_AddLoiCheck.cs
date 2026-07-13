using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoiCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileVersionLoiChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Verdict = table.Column<int>(type: "integer", nullable: false),
                    CoveragePercent = table.Column<double>(type: "double precision", nullable: false),
                    TotalElements = table.Column<int>(type: "integer", nullable: false),
                    ConformantElements = table.Column<int>(type: "integer", nullable: false),
                    ElementsWithUnknownType = table.Column<int>(type: "integer", nullable: false),
                    SchemaName = table.Column<string>(type: "text", nullable: true),
                    ParserUsed = table.Column<string>(type: "text", nullable: true),
                    MissingSummaryJson = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileVersionLoiChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileVersionLoiChecks_FileVersions_FileVersionId",
                        column: x => x.FileVersionId,
                        principalTable: "FileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoiFieldAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldNameNormalized = table.Column<string>(type: "text", nullable: false),
                    AliasNormalized = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoiFieldAliases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoiRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Discipline = table.Column<int>(type: "integer", nullable: false),
                    ComponentCode = table.Column<string>(type: "text", nullable: true),
                    ComponentName = table.Column<string>(type: "text", nullable: true),
                    FieldName = table.Column<string>(type: "text", nullable: false),
                    FieldNameNormalized = table.Column<string>(type: "text", nullable: false),
                    ParamGroup = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    IsCommon = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoiRequirements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileVersionLoiChecks_FileVersionId",
                table: "FileVersionLoiChecks",
                column: "FileVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoiFieldAliases_AliasNormalized",
                table: "LoiFieldAliases",
                column: "AliasNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_LoiFieldAliases_FieldNameNormalized_AliasNormalized",
                table: "LoiFieldAliases",
                columns: new[] { "FieldNameNormalized", "AliasNormalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoiRequirements_Discipline_ComponentCode",
                table: "LoiRequirements",
                columns: new[] { "Discipline", "ComponentCode" });

            migrationBuilder.CreateIndex(
                name: "IX_LoiRequirements_Discipline_IsCommon",
                table: "LoiRequirements",
                columns: new[] { "Discipline", "IsCommon" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileVersionLoiChecks");

            migrationBuilder.DropTable(
                name: "LoiFieldAliases");

            migrationBuilder.DropTable(
                name: "LoiRequirements");
        }
    }
}
