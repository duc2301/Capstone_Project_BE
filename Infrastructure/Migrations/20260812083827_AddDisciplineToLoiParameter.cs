using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDisciplineToLoiParameter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoiParameters_RuleSetId_NameNormalized",
                table: "LoiParameters");

            migrationBuilder.AddColumn<int>(
                name: "Discipline",
                table: "LoiParameters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"ALTER TABLE ""LoiParameters"" ALTER COLUMN ""Discipline"" DROP DEFAULT;");

            migrationBuilder.Sql(@"DELETE FROM ""LoiParameters"";");

            migrationBuilder.Sql(@"
                INSERT INTO ""LoiParameters""
                    (""Id"", ""RuleSetId"", ""Discipline"", ""Name"", ""NameNormalized"", ""ParamGroup"", ""OrderIndex"")
                SELECT
                    gen_random_uuid(),
                    src.""RuleSetId"",
                    src.""Discipline"",
                    src.""Name"",
                    src.""NameNormalized"",
                    src.""ParamGroup"",
                    (ROW_NUMBER() OVER (
                        PARTITION BY src.""RuleSetId"", src.""Discipline""
                        ORDER BY src.""ParamGroup"", src.first_id) - 1)
                FROM (
                    SELECT
                        ""RuleSetId"",
                        ""Discipline"",
                        ""ParamNameNormalized"" AS ""NameNormalized"",
                        MIN(""ParamName"") AS ""Name"",
                        MIN(""ParamGroup"") AS ""ParamGroup"",
                        MIN(""Id""::text) AS first_id
                    FROM ""LoiRequirements""
                    GROUP BY ""RuleSetId"", ""Discipline"", ""ParamNameNormalized""
                ) src;");

            migrationBuilder.CreateIndex(
                name: "IX_LoiParameters_RuleSetId_Discipline_NameNormalized",
                table: "LoiParameters",
                columns: new[] { "RuleSetId", "Discipline", "NameNormalized" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoiParameters_RuleSetId_Discipline_NameNormalized",
                table: "LoiParameters");

            migrationBuilder.DropColumn(
                name: "Discipline",
                table: "LoiParameters");

            migrationBuilder.CreateIndex(
                name: "IX_LoiParameters_RuleSetId_NameNormalized",
                table: "LoiParameters",
                columns: new[] { "RuleSetId", "NameNormalized" },
                unique: true);
        }
    }
}
