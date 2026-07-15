using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MergeFileVersionToFileVersionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileNotes_FileVersions_FileVersionId",
                table: "FileNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_FileVersionLoiChecks_FileVersions_FileVersionId",
                table: "FileVersionLoiChecks");

            migrationBuilder.DropForeignKey(
                name: "FK_MarkupSets_FileVersions_FileVersionId",
                table: "MarkupSets");

            migrationBuilder.AddColumn<string>(
                name: "CertificateSerial",
                table: "FileVersionStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "FileVersionStates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSigned",
                table: "FileVersionStates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreviewStoragePath",
                table: "FileVersionStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAt",
                table: "FileVersionStates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SignedBy",
                table: "FileVersionStates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAt",
                table: "FileVersionStates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByAccountId",
                table: "FileVersionStates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewerError",
                table: "FileVersionStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewerProgress",
                table: "FileVersionStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ViewerStatus",
                table: "FileVersionStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ViewerUrn",
                table: "FileVersionStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FileNotes_FileVersionStates_FileVersionId",
                table: "FileNotes",
                column: "FileVersionId",
                principalTable: "FileVersionStates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FileVersionLoiChecks_FileVersionStates_FileVersionId",
                table: "FileVersionLoiChecks",
                column: "FileVersionId",
                principalTable: "FileVersionStates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MarkupSets_FileVersionStates_FileVersionId",
                table: "MarkupSets",
                column: "FileVersionId",
                principalTable: "FileVersionStates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileNotes_FileVersionStates_FileVersionId",
                table: "FileNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_FileVersionLoiChecks_FileVersionStates_FileVersionId",
                table: "FileVersionLoiChecks");

            migrationBuilder.DropForeignKey(
                name: "FK_MarkupSets_FileVersionStates_FileVersionId",
                table: "MarkupSets");

            migrationBuilder.DropColumn(
                name: "CertificateSerial",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "IsSigned",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "PreviewStoragePath",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "SignedAt",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "SignedBy",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "UploadedByAccountId",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "ViewerError",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "ViewerProgress",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "ViewerStatus",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "ViewerUrn",
                table: "FileVersionStates");

            migrationBuilder.AddForeignKey(
                name: "FK_FileNotes_FileVersions_FileVersionId",
                table: "FileNotes",
                column: "FileVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FileVersionLoiChecks_FileVersions_FileVersionId",
                table: "FileVersionLoiChecks",
                column: "FileVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MarkupSets_FileVersions_FileVersionId",
                table: "MarkupSets",
                column: "FileVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
