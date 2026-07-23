using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJointVenture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<bool>(
                name: "IsJointVenture",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RepresentativeOrganizationId",
                table: "Organizations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JointVentureMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JointVentureId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberOrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JointVentureMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JointVentureMembers_Organizations_JointVentureId",
                        column: x => x.JointVentureId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JointVentureMembers_Organizations_MemberOrganizationId",
                        column: x => x.MemberOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JointVentureMembers_JointVentureId_MemberOrganizationId",
                table: "JointVentureMembers",
                columns: new[] { "JointVentureId", "MemberOrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JointVentureMembers_MemberOrganizationId",
                table: "JointVentureMembers",
                column: "MemberOrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JointVentureMembers");


            migrationBuilder.DropColumn(
                name: "IsJointVenture",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "RepresentativeOrganizationId",
                table: "Organizations");
        }
    }
}
