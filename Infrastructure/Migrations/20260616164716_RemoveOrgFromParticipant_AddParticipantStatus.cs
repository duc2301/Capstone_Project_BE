using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrgFromParticipant_AddParticipantStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectParticipants_Organizations_OrganizationId",
                table: "ProjectParticipants");

            migrationBuilder.DropIndex(
                name: "IX_ProjectParticipants_OrganizationId",
                table: "ProjectParticipants");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ProjectParticipants");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ProjectParticipants",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProjectParticipants");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ProjectParticipants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParticipants_OrganizationId",
                table: "ProjectParticipants",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectParticipants_Organizations_OrganizationId",
                table: "ProjectParticipants",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id");
        }
    }
}
