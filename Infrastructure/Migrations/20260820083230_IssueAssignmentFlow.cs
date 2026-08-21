using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IssueAssignmentFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueFileViewGrants");

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "Issues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentRejectReason",
                table: "Issues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignmentRespondedAt",
                table: "Issues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentRespondedByAccountId",
                table: "Issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignmentStatus",
                table: "Issues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueReminderSentAt",
                table: "Issues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueNotifiedAt",
                table: "Issues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(@"UPDATE ""Issues"" SET ""AssignmentStatus"" = 2, ""AssignedAt"" = ""CreatedAt"" WHERE ""AssignedToAccountId"" IS NOT NULL OR ""AssignedToGroupId"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "AssignmentRejectReason",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "AssignmentRespondedAt",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "AssignmentRespondedByAccountId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "AssignmentStatus",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "DueReminderSentAt",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "OverdueNotifiedAt",
                table: "Issues");

            migrationBuilder.CreateTable(
                name: "IssueFileViewGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
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
    }
}
