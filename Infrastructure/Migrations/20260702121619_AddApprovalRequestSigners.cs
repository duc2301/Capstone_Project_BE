using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalRequestSigners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromZone",
                table: "ApprovalRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSignature",
                table: "ApprovalRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TargetZone",
                table: "ApprovalRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ApprovalRequestSigners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    SignerGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CertificateSerial = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequestSigners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalRequestSigners_Accounts_SignerAccountId",
                        column: x => x.SignerAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalRequestSigners_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApprovalRequestSigners_Groups_SignerGroupId",
                        column: x => x.SignerGroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequestSigners_ApprovalRequestId",
                table: "ApprovalRequestSigners",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequestSigners_SignerAccountId",
                table: "ApprovalRequestSigners",
                column: "SignerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequestSigners_SignerGroupId",
                table: "ApprovalRequestSigners",
                column: "SignerGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalRequestSigners");

            migrationBuilder.DropColumn(
                name: "FromZone",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "RequiresSignature",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "TargetZone",
                table: "ApprovalRequests");
        }
    }
}
