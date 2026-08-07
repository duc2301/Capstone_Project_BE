using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveAiAnalysisToFileVersionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Thêm cột AI-analysis (per-version) vào FileVersionStates TRƯỚC.
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "FileVersionStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Warnning",
                table: "FileVersionStates",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarnningMessage",
                table: "FileVersionStates",
                type: "text",
                nullable: true);

            // 2) PRESERVE: copy dữ liệu cũ từ FileItems sang version HIỆN HÀNH (khỏi mất tóm tắt/cảnh báo đang có).
            migrationBuilder.Sql(@"
                UPDATE ""FileVersionStates"" v
                SET ""Description"" = f.""Description"",
                    ""Warnning"" = f.""Warnning"",
                    ""WarnningMessage"" = f.""WarnningMessage""
                FROM ""FileItems"" f
                WHERE v.""FileItemId"" = f.""Id"" AND v.""IsCurrent"" = TRUE;");

            // 3) Drop cột khỏi FileItems SAU KHI đã copy xong.
            migrationBuilder.DropColumn(
                name: "Description",
                table: "FileItems");

            migrationBuilder.DropColumn(
                name: "Warnning",
                table: "FileItems");

            migrationBuilder.DropColumn(
                name: "WarnningMessage",
                table: "FileItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1) Thêm lại cột trên FileItems.
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "FileItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Warnning",
                table: "FileItems",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarnningMessage",
                table: "FileItems",
                type: "text",
                nullable: true);

            // 2) Copy ngược từ version hiện hành về FileItems (giữ dữ liệu khi rollback).
            migrationBuilder.Sql(@"
                UPDATE ""FileItems"" f
                SET ""Description"" = v.""Description"",
                    ""Warnning"" = v.""Warnning"",
                    ""WarnningMessage"" = v.""WarnningMessage""
                FROM ""FileVersionStates"" v
                WHERE v.""FileItemId"" = f.""Id"" AND v.""IsCurrent"" = TRUE;");

            // 3) Drop khỏi FileVersionStates.
            migrationBuilder.DropColumn(
                name: "Description",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "Warnning",
                table: "FileVersionStates");

            migrationBuilder.DropColumn(
                name: "WarnningMessage",
                table: "FileVersionStates");
        }
    }
}
