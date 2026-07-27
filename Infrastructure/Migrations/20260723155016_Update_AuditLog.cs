using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Update_AuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DetailJson",
                table: "AuditLogs",
                newName: "Detail");

            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "AuditLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "Detail",
                table: "AuditLogs",
                newName: "DetailJson");
        }
    }
}
