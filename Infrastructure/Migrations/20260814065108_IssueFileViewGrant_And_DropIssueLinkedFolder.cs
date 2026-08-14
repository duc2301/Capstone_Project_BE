using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IssueFileViewGrant_And_DropIssueLinkedFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinkedFolderId",
                table: "Issues");

            migrationBuilder.CreateTable(
                name: "IssueFileViewGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueFileViewGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueFileViewGrants_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IssueFileViewGrants_FileItems_FileItemId",
                        column: x => x.FileItemId,
                        principalTable: "FileItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueFileViewGrants_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssueFileViewGrants_AccountId",
                table: "IssueFileViewGrants",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueFileViewGrants_FileItemId",
                table: "IssueFileViewGrants",
                column: "FileItemId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueFileViewGrants_IssueId_FileItemId_AccountId",
                table: "IssueFileViewGrants",
                columns: new[] { "IssueId", "FileItemId", "AccountId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueFileViewGrants");

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedFolderId",
                table: "Issues",
                type: "uuid",
                nullable: true);
        }
    }
}
