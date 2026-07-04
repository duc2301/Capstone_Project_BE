using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameMarkupScopeToIssue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarkupSets_ScopeType_ScopeId",
                table: "MarkupSets");

            migrationBuilder.DropColumn(
                name: "ScopeType",
                table: "MarkupSets");

            migrationBuilder.RenameColumn(
                name: "ScopeId",
                table: "MarkupSets",
                newName: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkupSets_IssueId",
                table: "MarkupSets",
                column: "IssueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarkupSets_IssueId",
                table: "MarkupSets");

            migrationBuilder.RenameColumn(
                name: "IssueId",
                table: "MarkupSets",
                newName: "ScopeId");

            migrationBuilder.AddColumn<int>(
                name: "ScopeType",
                table: "MarkupSets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MarkupSets_ScopeType_ScopeId",
                table: "MarkupSets",
                columns: new[] { "ScopeType", "ScopeId" });
        }
    }
}
