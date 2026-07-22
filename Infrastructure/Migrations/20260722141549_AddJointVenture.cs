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
            migrationBuilder.DropForeignKey(
                name: "FK_Organizations_Organizations_RepresentativeOrganizationId",
                table: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_RepresentativeOrganizationId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Organizations");

            migrationBuilder.AlterColumn<string>(
                name: "TaxCode",
                table: "Organizations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_OrganizationId",
                table: "Groups",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_Organizations_OrganizationId",
                table: "Groups",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Groups_Organizations_OrganizationId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Groups_OrganizationId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Groups");

            migrationBuilder.AlterColumn<string>(
                name: "TaxCode",
                table: "Organizations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_RepresentativeOrganizationId",
                table: "Organizations",
                column: "RepresentativeOrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Organizations_Organizations_RepresentativeOrganizationId",
                table: "Organizations",
                column: "RepresentativeOrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
