using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoiRuleSetAndParameters : Migration
    {
        private const string DefaultRuleSetId = "53000000-0000-0000-0000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoiRequirements_Discipline_ComponentCode",
                table: "LoiRequirements");

            migrationBuilder.DropIndex(
                name: "IX_LoiComponents_CodeNormalized",
                table: "LoiComponents");

            migrationBuilder.CreateTable(
                name: "LoiRuleSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoiRuleSets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoiRuleSets_IsDefault",
                table: "LoiRuleSets",
                column: "IsDefault");

            migrationBuilder.Sql($@"
                INSERT INTO ""LoiRuleSets""
                    (""Id"", ""Name"", ""Description"", ""IsDefault"", ""IsSystem"", ""CreatedByAccountId"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (
                    '{DefaultRuleSetId}',
                    'BXD 347 - Phụ lục 02 (công trình dân dụng)',
                    'Mức độ phát triển thông tin phi hình học theo QĐ 347/QĐ-BXD ngày 02/04/2021, Phụ lục 02.',
                    TRUE, TRUE, NULL, NOW(), NULL)
                ON CONFLICT (""Id"") DO NOTHING;");

            migrationBuilder.AddColumn<Guid>(
                name: "LoiRuleSetId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FieldOrder",
                table: "LoiRequirements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RuleSetId",
                table: "LoiRequirements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid(DefaultRuleSetId));

            migrationBuilder.AddColumn<Guid>(
                name: "RuleSetId",
                table: "LoiComponents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid(DefaultRuleSetId));

            migrationBuilder.Sql(@"
                ALTER TABLE ""LoiRequirements"" ALTER COLUMN ""RuleSetId"" DROP DEFAULT;
                ALTER TABLE ""LoiRequirements"" ALTER COLUMN ""FieldOrder"" DROP DEFAULT;
                ALTER TABLE ""LoiComponents"" ALTER COLUMN ""RuleSetId"" DROP DEFAULT;");

            migrationBuilder.CreateTable(
                name: "LoiParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NameNormalized = table.Column<string>(type: "text", nullable: false),
                    ParamGroup = table.Column<int>(type: "integer", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoiParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoiParameters_LoiRuleSets_RuleSetId",
                        column: x => x.RuleSetId,
                        principalTable: "LoiRuleSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoiParameters_RuleSetId_NameNormalized",
                table: "LoiParameters",
                columns: new[] { "RuleSetId", "NameNormalized" },
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO ""LoiParameters"" (""Id"", ""RuleSetId"", ""Name"", ""NameNormalized"", ""ParamGroup"", ""OrderIndex"")
                SELECT
                    gen_random_uuid(),
                    src.""RuleSetId"",
                    src.""Name"",
                    src.""NameNormalized"",
                    src.""ParamGroup"",
                    (ROW_NUMBER() OVER (PARTITION BY src.""RuleSetId"" ORDER BY src.""ParamGroup"", src.first_id) - 1)
                FROM (
                    SELECT
                        ""RuleSetId"",
                        ""ParamNameNormalized"" AS ""NameNormalized"",
                        MIN(""ParamName"") AS ""Name"",
                        MIN(""ParamGroup"") AS ""ParamGroup"",
                        MIN(""Id""::text) AS first_id
                    FROM ""LoiRequirements""
                    GROUP BY ""RuleSetId"", ""ParamNameNormalized""
                ) src
                ON CONFLICT DO NOTHING;");

            migrationBuilder.Sql(@"
                WITH grouped AS (
                    SELECT
                        ""RuleSetId"",
                        COALESCE(""ComponentCode"", '') AS component_code,
                        COALESCE(""Variant"", '') AS variant,
                        ""FieldNameNormalized"",
                        MIN(""Id""::text) AS first_id
                    FROM ""LoiRequirements""
                    GROUP BY ""RuleSetId"", COALESCE(""ComponentCode"", ''), COALESCE(""Variant"", ''), ""FieldNameNormalized""
                ),
                ranked AS (
                    SELECT
                        grouped.*,
                        (ROW_NUMBER() OVER (
                            PARTITION BY ""RuleSetId"", component_code, variant
                            ORDER BY first_id) - 1) AS field_order
                    FROM grouped
                )
                UPDATE ""LoiRequirements"" AS target
                SET ""FieldOrder"" = ranked.field_order
                FROM ranked
                WHERE target.""RuleSetId"" = ranked.""RuleSetId""
                  AND COALESCE(target.""ComponentCode"", '') = ranked.component_code
                  AND COALESCE(target.""Variant"", '') = ranked.variant
                  AND target.""FieldNameNormalized"" = ranked.""FieldNameNormalized"";");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_LoiRuleSetId",
                table: "Projects",
                column: "LoiRuleSetId");

            migrationBuilder.CreateIndex(
                name: "IX_LoiRequirements_RuleSetId_Discipline_ComponentCode",
                table: "LoiRequirements",
                columns: new[] { "RuleSetId", "Discipline", "ComponentCode" });

            migrationBuilder.CreateIndex(
                name: "IX_LoiComponents_RuleSetId_CodeNormalized",
                table: "LoiComponents",
                columns: new[] { "RuleSetId", "CodeNormalized" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LoiComponents_LoiRuleSets_RuleSetId",
                table: "LoiComponents",
                column: "RuleSetId",
                principalTable: "LoiRuleSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LoiRequirements_LoiRuleSets_RuleSetId",
                table: "LoiRequirements",
                column: "RuleSetId",
                principalTable: "LoiRuleSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_LoiRuleSets_LoiRuleSetId",
                table: "Projects",
                column: "LoiRuleSetId",
                principalTable: "LoiRuleSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoiComponents_LoiRuleSets_RuleSetId",
                table: "LoiComponents");

            migrationBuilder.DropForeignKey(
                name: "FK_LoiRequirements_LoiRuleSets_RuleSetId",
                table: "LoiRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_LoiRuleSets_LoiRuleSetId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "LoiParameters");

            migrationBuilder.DropTable(
                name: "LoiRuleSets");

            migrationBuilder.DropIndex(
                name: "IX_Projects_LoiRuleSetId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_LoiRequirements_RuleSetId_Discipline_ComponentCode",
                table: "LoiRequirements");

            migrationBuilder.DropIndex(
                name: "IX_LoiComponents_RuleSetId_CodeNormalized",
                table: "LoiComponents");

            migrationBuilder.DropColumn(
                name: "LoiRuleSetId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "FieldOrder",
                table: "LoiRequirements");

            migrationBuilder.DropColumn(
                name: "RuleSetId",
                table: "LoiRequirements");

            migrationBuilder.DropColumn(
                name: "RuleSetId",
                table: "LoiComponents");

            migrationBuilder.CreateIndex(
                name: "IX_LoiRequirements_Discipline_ComponentCode",
                table: "LoiRequirements",
                columns: new[] { "Discipline", "ComponentCode" });

            migrationBuilder.CreateIndex(
                name: "IX_LoiComponents_CodeNormalized",
                table: "LoiComponents",
                column: "CodeNormalized",
                unique: true);
        }
    }
}
