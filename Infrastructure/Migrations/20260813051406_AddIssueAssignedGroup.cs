using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueAssignedGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToGroupId",
                table: "Issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"UPDATE ""Issues"" SET ""Status"" = 1 WHERE ""Status"" = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedToGroupId",
                table: "Issues");
        }
    }
}
