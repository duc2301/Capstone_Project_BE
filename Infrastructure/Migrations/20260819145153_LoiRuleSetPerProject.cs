using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoiRuleSetPerProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoiRuleSets_IsDefault",
                table: "LoiRuleSets");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "LoiRuleSets");

            migrationBuilder.CreateIndex(
                name: "IX_LoiRuleSets_IsSystem",
                table: "LoiRuleSets",
                column: "IsSystem");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoiRuleSets_IsSystem",
                table: "LoiRuleSets");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "LoiRuleSets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_LoiRuleSets_IsDefault",
                table: "LoiRuleSets",
                column: "IsDefault");
        }
    }
}
