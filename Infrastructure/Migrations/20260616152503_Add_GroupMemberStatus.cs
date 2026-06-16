using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_GroupMemberStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileVersions_FileVersions_SourceFileVersionId",
                table: "FileVersions");

            migrationBuilder.DropIndex(
                name: "IX_FileVersions_SourceFileVersionId",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "SourceFileVersionId",
                table: "FileVersions");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "GroupMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Thành viên đã tồn tại (theo luồng cũ) đều đang Active -> set Status = 1 (Active), tránh bị mặc định Pending (0).
            migrationBuilder.Sql("UPDATE \"GroupMembers\" SET \"Status\" = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "GroupMembers");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceFileVersionId",
                table: "FileVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileVersions_SourceFileVersionId",
                table: "FileVersions",
                column: "SourceFileVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileVersions_FileVersions_SourceFileVersionId",
                table: "FileVersions",
                column: "SourceFileVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
