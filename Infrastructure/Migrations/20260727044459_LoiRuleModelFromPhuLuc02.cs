using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoiRuleModelFromPhuLuc02 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoiRequirements_Discipline_IsCommon",
                table: "LoiRequirements");

            migrationBuilder.DropColumn(
                name: "IsCommon",
                table: "LoiRequirements");

            migrationBuilder.AddColumn<string>(
                name: "ParamName",
                table: "LoiRequirements",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ParamNameNormalized",
                table: "LoiRequirements",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Variant",
                table: "LoiRequirements",
                type: "text",
                nullable: true);

            // Mặc định = LoiStages.Default (2 — Thiết kế cơ sở): bản ghi cũ chưa có khái niệm giai đoạn.
            migrationBuilder.AddColumn<int>(
                name: "TargetStage",
                table: "FileVersionLoiChecks",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateIndex(
                name: "IX_LoiRequirements_ParamNameNormalized",
                table: "LoiRequirements",
                column: "ParamNameNormalized");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoiRequirements_ParamNameNormalized",
                table: "LoiRequirements");

            migrationBuilder.DropColumn(
                name: "ParamName",
                table: "LoiRequirements");

            migrationBuilder.DropColumn(
                name: "ParamNameNormalized",
                table: "LoiRequirements");

            migrationBuilder.DropColumn(
                name: "Variant",
                table: "LoiRequirements");

            migrationBuilder.DropColumn(
                name: "TargetStage",
                table: "FileVersionLoiChecks");

            migrationBuilder.AddColumn<bool>(
                name: "IsCommon",
                table: "LoiRequirements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_LoiRequirements_Discipline_IsCommon",
                table: "LoiRequirements",
                columns: new[] { "Discipline", "IsCommon" });
        }
    }
}
